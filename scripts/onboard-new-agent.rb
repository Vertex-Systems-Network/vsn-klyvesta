#!/usr/bin/env ruby
# frozen_string_literal: true

require 'yaml'
require 'time'
require 'open3'

ROOT = File.expand_path('..', __dir__)
REGISTRY_PATH = ENV.fetch('AGENT_REGISTRY_PATH', File.join(ROOT, '.ai', 'parallel-branch-registry.yaml'))
WORK_ITEMS_ROOT = ENV.fetch('AGENT_WORK_ITEMS_ROOT', File.join(ROOT, '.ai', 'work-items'))
NO_SLOT_MESSAGE = 'Go Home Come Back Next Time'
SHA_PATTERN = /\A[0-9a-f]{40}\z/
BASELINE_BRANCH = 'parallel/integration-staging'

module AgentOnboarding
  module_function

  def load_yaml(path)
    YAML.safe_load(File.read(path), permitted_classes: [], aliases: false) || {}
  end

  def work_items(root)
    Dir.glob(File.join(root, '*', '*.yaml')).to_h do |path|
      data = load_yaml(path)
      [data['module'], [path, data]]
    end
  end

  def validate_arrival_branch!(branch)
    raise ArgumentError, 'New agent must start from main before assignment.' unless branch == 'main'
  end

  def validate_sha!(sha, label = 'SHA')
    raise ArgumentError, "#{label} must be a full 40-character lowercase SHA." unless sha.to_s.match?(SHA_PATTERN)
    sha
  end

  def git(root, *args)
    stdout, stderr, status = Open3.capture3('git', *args, chdir: root)
    [stdout.strip, stderr.strip, status.success?]
  end

  def resolve_baseline_sha(root)
    supplied = ENV['NEW_AGENT_BASELINE_SHA'].to_s.strip
    return validate_sha!(supplied, 'NEW_AGENT_BASELINE_SHA') unless supplied.empty?

    ['refs/remotes/origin/parallel/integration-staging', 'refs/heads/parallel/integration-staging'].each do |ref|
      sha, = git(root, 'rev-parse', '--verify', ref)
      return validate_sha!(sha, 'accepted integration baseline') if sha.match?(SHA_PATTERN)
    end

    raise ArgumentError, 'Unable to resolve parallel/integration-staging. Fetch the accepted baseline before assignment.'
  end

  def branch_contains_baseline?(root, branch, baseline_sha)
    refs = ["refs/remotes/origin/#{branch}", "refs/heads/#{branch}"]
    ref = refs.find { |candidate| git(root, 'show-ref', '--verify', '--quiet', candidate)[2] }
    return false unless ref

    git(root, 'merge-base', '--is-ancestor', baseline_sha, ref)[2]
  end

  def free_slots(registry, items)
    Array(registry['branches']).select do |entry|
      item = items.dig(entry['module'], 1)
      entry['status'] == 'READY' && entry['occupancy'] == 'OPEN' && item && item['status'] == 'READY'
    end.sort_by { |entry| [entry.fetch('merge_order_group', 999), entry['module'].to_s] }
  end

  def assign!(registry, items, slot, agent_name, started_at, baseline_sha)
    raise ArgumentError, 'Agent name is required.' if agent_name.to_s.strip.empty?
    validate_sha!(baseline_sha, 'accepted baseline SHA')

    slot['occupancy'] = 'OCCUPIED'
    slot['agent_name'] = agent_name
    slot['start_status'] = 'ASSIGNED'
    slot['status'] = 'ACTIVE'
    slot['started_at'] = started_at
    slot['accepted_baseline_sha'] = baseline_sha

    path, item = items.fetch(slot['module'])
    item['assigned_agent'] = agent_name
    item['start_status'] = 'ASSIGNED'
    item['status'] = 'ACTIVE'
    item['started_at'] = started_at
    item['base_sha'] = baseline_sha
    item['accepted_baseline_sha'] = baseline_sha
    [path, item]
  end
end

if $PROGRAM_NAME == __FILE__
  agent_name = ENV.fetch('NEW_AGENT_NAME', '').strip
  arrival_branch = ENV.fetch('NEW_AGENT_START_BRANCH', '').strip

  begin
    AgentOnboarding.validate_arrival_branch!(arrival_branch)
  rescue ArgumentError => e
    warn e.message
    exit 2
  end

  if agent_name.empty?
    warn 'NEW_AGENT_NAME is required.'
    exit 2
  end

  registry = AgentOnboarding.load_yaml(REGISTRY_PATH)
  items = AgentOnboarding.work_items(WORK_ITEMS_ROOT)
  slot = AgentOnboarding.free_slots(registry, items).first

  if slot.nil?
    puts NO_SLOT_MESSAGE
    exit 3
  end

  begin
    baseline_sha = AgentOnboarding.resolve_baseline_sha(ROOT)
  rescue ArgumentError => e
    warn e.message
    exit 2
  end

  unless AgentOnboarding.branch_contains_baseline?(ROOT, slot['branch'], baseline_sha)
    warn "Assigned branch #{slot['branch']} does not contain accepted baseline #{baseline_sha}. Supervisor must refresh it before assignment."
    exit 4
  end

  puts "ASSIGNMENT: #{agent_name} -> #{slot['module']} (#{slot['branch']})"
  puts 'START_FROM: main'
  puts "ACCEPTED_BASELINE: #{baseline_sha}"
  puts "THEN_CHECKOUT: #{slot['branch']}"

  unless ARGV.include?('--apply')
    puts 'DRY_RUN: plan was not modified.'
    exit 0
  end

  started_at = ENV.fetch('NEW_AGENT_START_TIME', Time.now.utc.iso8601)
  work_item_path, work_item = AgentOnboarding.assign!(registry, items, slot, agent_name, started_at, baseline_sha)
  File.write(REGISTRY_PATH, YAML.dump(registry))
  File.write(work_item_path, YAML.dump(work_item))
  puts "PLAN_UPDATED: #{slot['module']} is OCCUPIED by #{agent_name} with start_status ASSIGNED at baseline #{baseline_sha}."
end
