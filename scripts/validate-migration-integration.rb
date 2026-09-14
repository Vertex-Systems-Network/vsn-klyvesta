#!/usr/bin/env ruby
# frozen_string_literal: true

require 'open3'

ROOT = File.expand_path('..', __dir__)
DB_BRANCH = 'parallel/database-integration'

def branch_name
  [ENV['AGENT_HEAD_BRANCH'], ENV['GITHUB_HEAD_REF'], ENV['GITHUB_REF_NAME']].compact.map(&:strip).find { |v| !v.empty? } || ''
end

def base_ref
  explicit = ENV['AGENT_BASE_REF'].to_s.strip
  return explicit unless explicit.empty?
  system('git', 'show-ref', '--verify', '--quiet', 'refs/remotes/origin/parallel/integration-staging', chdir: ROOT) ? 'origin/parallel/integration-staging' : 'origin/main'
end

def changed_files
  supplied = ENV['AGENT_CHANGED_FILES'].to_s
  return supplied.lines.map(&:strip).reject(&:empty?) unless supplied.empty?
  stdout, stderr, status = Open3.capture3('git', 'diff', '--name-only', "#{base_ref}...HEAD", chdir: ROOT)
  abort "Unable to inspect migration delta: #{stderr}" unless status.success?
  stdout.lines.map(&:strip).reject(&:empty?)
end

migration_delta = changed_files.select { |path| path.include?('/Migrations/') || path.end_with?('ModelSnapshot.cs') }
branch = branch_name
if migration_delta.any? && branch != DB_BRANCH
  warn "Migration ownership violation: only #{DB_BRANCH} may change migrations/model snapshots in parallel mode."
  migration_delta.each { |path| warn " - #{path}" }
  exit 1
end

migration_files = Dir.glob(File.join(ROOT, '**', 'Migrations', '*.cs')).reject { |path| path.end_with?('.Designer.cs') || path.end_with?('ModelSnapshot.cs') }
ids = migration_files.group_by { |path| File.basename(path).split('_', 2).first }
duplicates = ids.select { |id, paths| !id.empty? && paths.length > 1 }
unless duplicates.empty?
  warn 'Migration integration validation FAILED: duplicate migration IDs detected.'
  duplicates.each { |id, paths| warn " - #{id}: #{paths.map { |p| p.delete_prefix("#{ROOT}/") }.join(', ')}" }
  exit 1
end

puts "Migration integration validation PASS. Delta migrations/snapshots: #{migration_delta.length}."
