#!/usr/bin/env ruby
# frozen_string_literal: true

require 'yaml'
require 'open3'

ROOT = File.expand_path('..', __dir__)
MANIFEST = File.join(ROOT, '.ai', 'integration-baseline.yaml')
SHA = /\A[0-9a-f]{40}\z/

def git(*args)
  stdout, stderr, status = Open3.capture3('git', *args, chdir: ROOT)
  [stdout.strip, stderr.strip, status.success?]
end

def head_branch
  [ENV['AGENT_HEAD_BRANCH'], ENV['GITHUB_HEAD_REF'], ENV['GITHUB_REF_NAME']].compact.map(&:strip).find { |v| !v.empty? } || ''
end

manifest = YAML.safe_load(File.read(MANIFEST), permitted_classes: [], aliases: false) || {}
errors = []
baseline_branch = manifest['baseline_branch']
main_anchor = manifest['main_anchor_sha'].to_s
recorded = manifest['last_verified_baseline_sha'].to_s
errors << 'baseline_branch must be parallel/integration-staging' unless baseline_branch == 'parallel/integration-staging'
errors << 'main_anchor_sha must be a full SHA' unless main_anchor.match?(SHA)
errors << 'last_verified_baseline_sha must be a full SHA' unless recorded.match?(SHA)
errors << 'generation must be a non-negative integer' unless manifest['generation'].is_a?(Integer) && manifest['generation'] >= 0

remote_ref = "refs/remotes/origin/#{baseline_branch}"
local_ref = "refs/heads/#{baseline_branch}"
ref = if git('show-ref', '--verify', '--quiet', remote_ref)[2]
        remote_ref
      elsif git('show-ref', '--verify', '--quiet', local_ref)[2]
        local_ref
      end

if ref
  baseline_sha, _, ok = git('rev-parse', ref)
  errors << 'unable to resolve integration baseline ref' unless ok && baseline_sha.match?(SHA)
  if ok
    errors << 'main anchor is not an ancestor of integration baseline' unless git('merge-base', '--is-ancestor', main_anchor, baseline_sha)[2]
    branch = head_branch
    if branch.start_with?('parallel/') && branch != baseline_branch
      errors << "#{branch} is stale: current integration baseline #{baseline_sha} is not an ancestor of HEAD" unless git('merge-base', '--is-ancestor', baseline_sha, 'HEAD')[2]
    end
    if recorded != baseline_sha
      puts "BASELINE_RECORD_NOTICE: manifest records #{recorded}; current #{baseline_branch} is #{baseline_sha}. Supervisor should refresh documentary SHA after accepted advance."
    end
    puts "CURRENT_BASELINE_SHA: #{baseline_sha}"
  end
else
  puts 'BASELINE_REF_NOTICE: integration baseline remote ref is not present in this checkout; structural validation only.'
end

unless errors.empty?
  warn "Integration baseline validation FAILED (#{errors.length}):"
  errors.each { |e| warn " - #{e}" }
  exit 1
end

puts 'Integration baseline validation PASS.'
