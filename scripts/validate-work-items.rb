#!/usr/bin/env ruby
# frozen_string_literal: true

require 'yaml'

ROOT = File.expand_path('..', __dir__)
ORCHESTRATION_PATH = File.join(ROOT, '.ai', 'agent-orchestration.yaml')
REGISTRY_PATH = File.join(ROOT, '.ai', 'parallel-branch-registry.yaml')
WORK_ITEMS_ROOT = File.join(ROOT, '.ai', 'work-items')
SHA_PATTERN = /\A[0-9a-f]{40}\z/
VALID_STATUSES = %w[RESERVED READY ACTIVE BLOCKED VERIFYING VERIFIED SUBMITTED INTEGRATED].freeze


def yaml(path)
  YAML.safe_load(File.read(path), permitted_classes: [], aliases: false) || {}
end


def error(errors, file, message)
  errors << "#{file}: #{message}"
end

orchestration = yaml(ORCHESTRATION_PATH)
registry = yaml(REGISTRY_PATH)
module_rules = orchestration.fetch('modules', {})
registry_by_module = Array(registry['branches']).to_h { |entry| [entry['module'], entry] }
required_fields = Array(orchestration.dig('work_item_contract', 'required_fields'))
errors = []
items = []
ids = {}
modules = {}

Dir.glob(File.join(WORK_ITEMS_ROOT, '*', '*.yaml')).sort.each do |path|
  relative = path.delete_prefix("#{ROOT}/")
  item = yaml(path)
  items << [relative, item]

  required_fields.each do |field|
    error(errors, relative, "missing required field #{field}") unless item.key?(field)
  end

  id = item['id']
  mod = item['module']
  branch = item['branch']
  status = item['status']

  if ids.key?(id)
    error(errors, relative, "duplicate work-item id #{id} also used by #{ids[id]}")
  else
    ids[id] = relative
  end

  if modules.key?(mod)
    error(errors, relative, "multiple active registry files for module #{mod}: #{modules[mod]}")
  else
    modules[mod] = relative
  end

  error(errors, relative, "unknown module #{mod}") unless module_rules.key?(mod) || mod == 'supervisor-platform'

  registry_entry = registry_by_module[mod]
  if registry_entry.nil?
    error(errors, relative, "module #{mod} is missing from branch registry")
  else
    error(errors, relative, "branch #{branch} does not match registry #{registry_entry['branch']}") unless branch == registry_entry['branch']
    error(errors, relative, "agent_slot #{item['agent_slot']} does not match registry #{registry_entry['agent_slot']}") unless item['agent_slot'] == registry_entry['agent_slot']

    registry_status = registry_entry['status']
    if %w[BLOCKED RESERVED].include?(registry_status) && !%w[BLOCKED RESERVED].include?(status)
      error(errors, relative, "work-item status #{status} cannot outrun registry status #{registry_status}")
    end
  end

  error(errors, relative, "invalid status #{status}") unless VALID_STATUSES.include?(status)
  error(errors, relative, 'base_sha must be a full 40-character lowercase SHA') unless item['base_sha'].to_s.match?(SHA_PATTERN)
  error(errors, relative, 'accepted_baseline_sha must be a full 40-character lowercase SHA') unless item['accepted_baseline_sha'].to_s.match?(SHA_PATTERN)

  dependencies = Array(item['dependencies'])
  dependency_heads = item['dependency_heads'] || {}
  expected_dependencies = mod == 'supervisor-platform' ? [] : Array(module_rules.dig(mod, 'dependencies'))
  unless dependencies.sort == expected_dependencies.sort
    error(errors, relative, "dependencies #{dependencies.inspect} do not match orchestration #{expected_dependencies.inspect}")
  end

  unless dependency_heads.keys.sort == dependencies.sort
    error(errors, relative, "dependency_heads keys #{dependency_heads.keys.inspect} must exactly match dependencies #{dependencies.inspect}")
  end

  dependency_heads.each do |dependency, head|
    next if head.nil? && %w[BLOCKED RESERVED].include?(status)
    error(errors, relative, "dependency #{dependency} head must be a full SHA") unless head.to_s.match?(SHA_PATTERN)
  end

  blockers = Array(item['blockers'])
  if status == 'BLOCKED' && blockers.empty?
    error(errors, relative, 'BLOCKED work item must record at least one blocker')
  elsif %w[READY ACTIVE VERIFYING VERIFIED SUBMITTED INTEGRATED].include?(status) && !blockers.empty?
    error(errors, relative, "#{status} work item cannot retain blockers")
  end

  if %w[READY ACTIVE].include?(status) && dependencies.any?
    unresolved = dependencies.select { |dependency| dependency_heads[dependency].nil? }
    error(errors, relative, "#{status} work item has unresolved dependency heads: #{unresolved.join(', ')}") unless unresolved.empty?
  end

  if status == 'SUBMITTED'
    error(errors, relative, 'SUBMITTED work item requires submission_pr') if item['submission_pr'].nil?
  end

  if status == 'INTEGRATED'
    error(errors, relative, 'INTEGRATED work item requires integration_baseline_sha') unless item['integration_baseline_sha'].to_s.match?(SHA_PATTERN)
  end

  work_item_path = ".ai/work-items/#{mod}/**"
  unless Array(item['allowed_paths']).include?(work_item_path)
    error(errors, relative, "allowed_paths must include owned work-item path #{work_item_path}")
  end
end

if items.empty?
  errors << 'no work-item YAML files discovered'
end

registry_by_module.each_key do |mod|
  next if modules.key?(mod)
  errors << "branch registry module #{mod} has no durable work item"
end

unless errors.empty?
  warn "Work-item validation FAILED (#{errors.length} issue(s)):"
  errors.each { |message| warn " - #{message}" }
  exit 1
end

ready = items.select { |_path, item| item['status'] == 'READY' }.map { |_path, item| item['module'] }.sort
blocked = items.select { |_path, item| item['status'] == 'BLOCKED' }.map { |_path, item| item['module'] }.sort
active = items.select { |_path, item| item['status'] == 'ACTIVE' }.map { |_path, item| item['module'] }.sort

puts "Work-item validation PASS (#{items.length} work items)."
puts "READY: #{ready.join(', ')}"
puts "ACTIVE: #{active.join(', ')}"
puts "BLOCKED: #{blocked.join(', ')}"
