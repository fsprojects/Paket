source 'https://rubygems.org'

group :development do
  gem 'guard'
  gem 'guard-bundler'
  gem 'guard-depend'

  case RbConfig::CONFIG['target_os']
  when /windows|bccwin|cygwin|djgpp|mingw|mswin|wince/i
    gem 'ruby_gntp', require: false
    gem 'wdm', require: false
  when /linux/i
    gem 'rb-inotify', require: false
  when /mac|darwin/i
    gem 'rb-fsevent', require: false
    gem 'growl', require: false
  end
end
