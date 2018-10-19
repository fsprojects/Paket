#!/usr/bin/env bash

set -e

./build.sh GenerateDocs

npm install http-server broken-link-checker
node_modules/.bin/http-server docs/output --silent &
printf 'Started web server PID %s\n' $!

# Ignore errors to be able to kill the web server.
node_modules/.bin/blc --get --recursive --filter-level 3 http://localhost:8080 || true

[[ -n "$!" ]] && kill -9 $!
