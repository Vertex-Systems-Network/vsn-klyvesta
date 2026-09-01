#!/usr/bin/env ruby
# frozen_string_literal: true

require 'yaml'
require 'open3'

ROOT = File.expand_path('..', __dir__)
ORCHESTRATION_PATH = File.join(ROOT, '.ai', 'agent-orchestration.yaml')
REGISTRY_PATH = File.join(ROOT, '.ai', 'parallel-branch-registry.yaml')
FNM_FLAGS = File::FNM_PATHNAME | File::FNM_EXTGLOB

def load_yaml(path)
  YAML.safe_load(File.read(path), permitted_classes: [], aliases: false) || {}
end

def branch_name
  [ENV['AGENT_HEAD_BRANCH'], ENV['GITHUB_HEAD_REF'], ENV['GITHUB_REF_NAME']].compact.map(&:strip).find { |v| !v.empty? } || ''
end

def default_base_ref
  explicit = ENV['AGENT_BASE_REF'].to_s.strip
  return explicit unless explicit.empty?
  remote = 'refs/remotes/origin/parallel/integration-staging'
  return 'origin/parallel/integration-staging' if system('git', 'show-ref', '--verify', '--quiet', remote, chdir: ROOT)
  'origin/main'
end

def changed_files
  supplied = ENV['AGENT_CHANGED_FILES'].to_s
  return supplied.lines.map(&:strip).reject(&:empty?).uniq unless supplied.empty?
  base_ref = default_base_ref
  stdout, stderr, status = Open3.capture3('git', 'diff', '--name-only', "#{base_ref}...HEAD", chdir: ROOT)
  unless status.success?
    warn "Unable to determine changed files against #{base_ref}: #{stderr}"
    exit 2
  end
  stdout.lines.map(&:strip).reject(&:empty?).uniq
end

def matches?(pattern, path)
  File.fnmatch?(pattern, path, FNM_FLAGS)
end

def any_match?(patterns, path)
  Array(patterns).any? { |pattern| matches?(pattern, path) }
end

orchestration = load_yaml(ORCHESTRATION_PATH)
registry = load_yaml(REGISTRY_PATH)
branch = branch_name
if branch.empty?
  warn 'Unable to determine current agent branch.'
  exit 2
end
unless branch.start_with?('parallel/')
  puts "Ownership validator: #{branch} is not a canonical parallel branch; strict ownership enforcement skipped."
  exit 0
end

entry = Array(registry['branches']).find { |item| item['branch'] == branch }
if entry.nil?
  warn "Ownership violation: parallel branch #{branch} is not registered in .ai/parallel-branch-registry.yaml"
  exit 1
end
module_name = entry.fetch('module')
status = entry.fetch('status', 'RESERVED')
module_rules = orchestration.fetch('modules', {})[module_name]
role = entry.fetch('agent_slot', '').start_with?('supervisor') ? 'supervisor' : module_rules&.fetch('owner_role', nil)
files = changed_files
owned_work_item_pattern = ".ai/work-items/#{module_name}/**"
puts "Branch: #{branch}"
puts "Module: #{module_name}"
puts "Registry status: #{status}"
puts "Changed files: #{files.length}"

if role == 'supervisor'
  forbidden_source = files.select { |path| path.start_with?('src/') }
  unless forbidden_source.empty?
    warn 'Supervisor/platform ownership violation: module implementation source must stay on its owning module branch:'
    forbidden_source.each { |path| warn " - #{path}" }
    exit 1
  end
  puts 'Supervisor/platform branch ownership check PASS.'
  exit 0
end
if module_rules.nil?
  warn "Ownership violation: module #{module_name} has no orchestration rules."
  exit 1
end
if %w[BLOCKED RESERVED].include?(status)
  substantive = files.reject { |path| path.start_with?('.ai/checkpoints/') || matches?(owned_work_item_pattern, path) }
  unless substantive.empty?
    warn "Readiness violation: #{module_name} is #{status}; substantive implementation changes are not permitted yet."
    substantive.each { |path| warn " - #{path}" }
    exit 1
  end
end
allowed = Array(module_rules['allowed_paths']) + ['.ai/checkpoints/**', owned_work_item_pattern]
violations = files.reject { |path| any_match?(allowed, path) }
unless violations.empty?
  warn "Ownership violation: #{branch} changed paths outside module #{module_name} ownership:"
  violations.each { |path| warn " - #{path}" }
  warn 'Request an explicit shared-path grant from Supervisor/Integration instead of editing outside ownership.'
  exit 1
end
puts "Ownership validator PASS for #{module_name}."
