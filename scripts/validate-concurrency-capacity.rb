#!/usr/bin/env ruby
# frozen_string_literal: true

require 'yaml'
require_relative 'onboard-new-agent'

ROOT = File.expand_path('..', __dir__)
ORCHESTRATION = YAML.safe_load(File.read(File.join(ROOT, '.ai', 'agent-orchestration.yaml')), permitted_classes: [], aliases: false) || {}
REGISTRY = YAML.safe_load(File.read(File.join(ROOT, '.ai', 'parallel-branch-registry.yaml')), permitted_classes: [], aliases: false) || {}
ITEMS = AgentOnboarding.work_items(File.join(ROOT, '.ai', 'work-items'))
errors = []
open_slots = AgentOnboarding.free_slots(REGISTRY, ITEMS)
occupied_parallel = Array(REGISTRY['branches']).count { |e| e['occupancy'] == 'OCCUPIED' && e['module'] != 'supervisor-platform' }
capacity = open_slots.length + occupied_parallel
min = ORCHESTRATION.dig('concurrency', 'current_recommended_min').to_i
max = ORCHESTRATION.dig('concurrency', 'current_recommended_max').to_i
errors << "configured parallel capacity #{capacity} is below recommended minimum #{min}" if capacity < min
errors << "configured parallel capacity #{capacity} exceeds current maximum #{max}" if capacity > max
errors << 'required no-slot phrase drifted' unless ORCHESTRATION.dig('new_agent_onboarding', 'no_slot_message') == NO_SLOT_MESSAGE

simulation = Marshal.load(Marshal.dump(REGISTRY))
simulation['branches'].each do |entry|
  entry['occupancy'] = 'OCCUPIED' if entry['status'] == 'READY'
end
errors << 'overflow simulation should expose zero free slots' unless AgentOnboarding.free_slots(simulation, ITEMS).empty?

unless errors.empty?
  warn "Concurrency capacity validation FAILED (#{errors.length}):"
  errors.each { |e| warn " - #{e}" }
  exit 1
end

puts "Concurrency capacity validation PASS. Configured module capacity: #{capacity}; OPEN now: #{open_slots.length}; OCCUPIED module slots: #{occupied_parallel}."
puts "Overflow response: #{NO_SLOT_MESSAGE}"
