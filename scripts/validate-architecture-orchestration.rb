#!/usr/bin/env ruby
# frozen_string_literal: true

require 'yaml'

ROOT = File.expand_path('..', __dir__)
ORCHESTRATION = YAML.safe_load(File.read(File.join(ROOT, '.ai', 'agent-orchestration.yaml')), permitted_classes: [], aliases: false) || {}
REGISTRY = YAML.safe_load(File.read(File.join(ROOT, '.ai', 'parallel-branch-registry.yaml')), permitted_classes: [], aliases: false) || {}
modules = ORCHESTRATION.fetch('modules', {})
errors = []

branches = {}
patterns = {}
modules.each do |name, rule|
  branch = rule['canonical_branch']
  errors << "#{name}: missing canonical branch" if branch.to_s.empty?
  errors << "canonical branch #{branch} is owned by both #{branches[branch]} and #{name}" if branches.key?(branch)
  branches[branch] = name

  Array(rule['allowed_paths']).each do |pattern|
    if patterns.key?(pattern)
      errors << "allowed path pattern #{pattern} is duplicated by #{patterns[pattern]} and #{name}"
    else
      patterns[pattern] = name
    end
  end

  Array(rule['dependencies']).each do |dependency|
    errors << "#{name}: unknown dependency #{dependency}" unless modules.key?(dependency)
    errors << "#{name}: cannot depend on itself" if dependency == name
  end
end

visiting = {}
visited = {}
visit = lambda do |name, stack|
  return if visited[name]
  if visiting[name]
    errors << "dependency cycle detected: #{(stack + [name]).join(' -> ')}"
    return
  end
  visiting[name] = true
  Array(modules.dig(name, 'dependencies')).each { |dep| visit.call(dep, stack + [name]) if modules.key?(dep) }
  visiting.delete(name)
  visited[name] = true
end
modules.each_key { |name| visit.call(name, []) }

valid_occupancy = %w[OPEN OCCUPIED UNAVAILABLE]
seen_agents = {}
Array(REGISTRY['branches']).each do |entry|
  mod = entry['module']
  unless mod == 'supervisor-platform'
    errors << "registry module #{mod} is missing orchestration rules" unless modules.key?(mod)
    errors << "registry branch mismatch for #{mod}" if modules.key?(mod) && modules.dig(mod, 'canonical_branch') != entry['branch']
  end
  occupancy = entry['occupancy']
  errors << "#{mod}: invalid occupancy #{occupancy}" unless valid_occupancy.include?(occupancy)
  if %w[BLOCKED RESERVED].include?(entry['status']) && occupancy != 'UNAVAILABLE'
    errors << "#{mod}: #{entry['status']} slot must be UNAVAILABLE"
  end
  if entry['status'] == 'READY' && !%w[OPEN OCCUPIED].include?(occupancy)
    errors << "#{mod}: READY slot must be OPEN or OCCUPIED"
  end
  if occupancy == 'OPEN' && !entry['agent_name'].nil?
    errors << "#{mod}: OPEN slot cannot have agent_name"
  end
  if occupancy == 'OCCUPIED'
    agent = entry['agent_name'].to_s.strip
    errors << "#{mod}: OCCUPIED slot requires agent_name" if agent.empty?
    if !agent.empty? && seen_agents.key?(agent)
      errors << "agent #{agent} occupies multiple slots: #{seen_agents[agent]} and #{mod}"
    else
      seen_agents[agent] = mod unless agent.empty?
    end
  end
end

unless errors.empty?
  warn "Architecture/orchestration validation FAILED (#{errors.length}):"
  errors.each { |e| warn " - #{e}" }
  exit 1
end

puts "Architecture/orchestration validation PASS (#{modules.length} modules; dependency DAG acyclic)."
