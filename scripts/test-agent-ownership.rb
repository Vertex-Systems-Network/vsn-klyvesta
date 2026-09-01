#!/usr/bin/env ruby
# frozen_string_literal: true

require 'open3'

ROOT = File.expand_path('..', __dir__)
VALIDATOR = File.join(ROOT, 'scripts', 'validate-agent-ownership.rb')

Case = Struct.new(:name, :branch, :files, :expected_success, keyword_init: true)

cases = [
  Case.new(
    name: 'ready module accepts owned path',
    branch: 'parallel/ledger',
    files: "src/Klyvesta.Domain/Ledger/LedgerEntry.cs\n.ai/checkpoints/ledger.md\n",
    expected_success: true
  ),
  Case.new(
    name: 'module rejects another module path',
    branch: 'parallel/ledger',
    files: "src/Klyvesta.Application/Risk/PaperRiskGovernor.cs\n",
    expected_success: false
  ),
  Case.new(
    name: 'blocked module rejects substantive implementation',
    branch: 'parallel/risk',
    files: "src/Klyvesta.Application/Risk/PaperRiskGovernor.cs\n",
    expected_success: false
  ),
  Case.new(
    name: 'supervisor rejects module source takeover',
    branch: 'parallel/supervisor-platform',
    files: "src/Klyvesta.Application/Notifications/NotificationService.cs\n",
    expected_success: false
  ),
  Case.new(
    name: 'unregistered parallel branch is rejected',
    branch: 'parallel/not-registered',
    files: ".ai/checkpoints/test.md\n",
    expected_success: false
  )
]

failures = []

cases.each do |test_case|
  env = {
    'AGENT_HEAD_BRANCH' => test_case.branch,
    'AGENT_CHANGED_FILES' => test_case.files,
    'AGENT_BASE_REF' => 'origin/main'
  }

  stdout, stderr, status = Open3.capture3(env, 'ruby', VALIDATOR, chdir: ROOT)
  actual_success = status.success?
  outcome = actual_success == test_case.expected_success ? 'PASS' : 'FAIL'
  puts "#{outcome}: #{test_case.name}"

  next if outcome == 'PASS'

  failures << test_case.name
  warn stdout unless stdout.empty?
  warn stderr unless stderr.empty?
end

unless failures.empty?
  warn "Ownership validator self-test failures: #{failures.join(', ')}"
  exit 1
end

puts "Ownership validator self-tests PASS (#{cases.length}/#{cases.length})."
