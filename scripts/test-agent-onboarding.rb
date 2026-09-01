#!/usr/bin/env ruby
# frozen_string_literal: true

require_relative 'onboard-new-agent'

failures = []

def assert(failures, condition, message)
  failures << message unless condition
end

begin
  AgentOnboarding.validate_arrival_branch!('main')
rescue StandardError
  failures << 'main arrival branch should be accepted'
end

begin
  AgentOnboarding.validate_arrival_branch!('feature/test')
  failures << 'non-main arrival branch should be rejected'
rescue ArgumentError
  # expected
end

registry = {
  'branches' => [
    {'module' => 'zeta', 'branch' => 'parallel/zeta', 'status' => 'READY', 'occupancy' => 'OPEN', 'merge_order_group' => 20},
    {'module' => 'alpha', 'branch' => 'parallel/alpha', 'status' => 'READY', 'occupancy' => 'OPEN', 'merge_order_group' => 10}
  ]
}
items = {
  'zeta' => ['/tmp/zeta.yaml', {'module' => 'zeta', 'status' => 'READY'}],
  'alpha' => ['/tmp/alpha.yaml', {'module' => 'alpha', 'status' => 'READY'}]
}
slot = AgentOnboarding.free_slots(registry, items).first
assert(failures, slot && slot['module'] == 'alpha', 'lowest merge-order free slot should be selected first')

path, assigned = AgentOnboarding.assign!(registry, items, slot, 'agent-new', '2026-09-02T00:00:00Z')
assert(failures, path == '/tmp/alpha.yaml', 'assignment should update selected work item')
assert(failures, slot['occupancy'] == 'OCCUPIED', 'assigned slot should become OCCUPIED')
assert(failures, slot['agent_name'] == 'agent-new', 'assigned slot should record agent name')
assert(failures, slot['start_status'] == 'ASSIGNED', 'assigned slot should record start status')
assert(failures, assigned['status'] == 'ACTIVE', 'assigned work item should become ACTIVE')

registry['branches'].each { |entry| entry['occupancy'] = 'OCCUPIED' }
assert(failures, AgentOnboarding.free_slots(registry, items).empty?, 'fully occupied plan should expose no free slot')
assert(failures, NO_SLOT_MESSAGE == 'Go Home Come Back Next Time', 'no-slot response must match exact required phrase')

if failures.any?
  warn "Onboarding self-test FAILED (#{failures.length}):"
  failures.each { |failure| warn " - #{failure}" }
  exit 1
end

puts 'Onboarding self-test PASS.'
puts "NO_SLOT_MESSAGE: #{NO_SLOT_MESSAGE}"
