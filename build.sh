#!/usr/bin/env bash
if test "$OS" = "Windows_NT"
then
  # use .Net
  dotnet tool restore
  dotnet paket restore
  exit_code=$?
  if [ $exit_code -ne 0 ]; then
  	exit $exit_code
  fi
  MSBuild=`pwd -W`/packages/build/RoslynTools.MSBuild/tools/msbuild/MSBuild.exe packages/build/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx
else
  dotnet tool restore
  dotnet paket restore
  exit_code=$?
  if [ $exit_code -ne 0 ]; then
    certificate_count=$(certmgr -list -c Trust | grep X.509 | wc -l)
    if [ $certificate_count -le 1 ]; then
      echo "Couldn't download Paket. This might be because your Mono installation"
      echo "doesn't have the right SSL root certificates installed. One way"
      echo "to fix this would be to download the list of SSL root certificates"
      echo "from the Mozilla project by running the following command:"
      echo ""
      echo "    mozroots --import --sync"
      echo ""
      echo "This will import over 100 SSL root certificates into your Mono"
      echo "certificate repository. Then try running the build script again."
    fi
    exit $exit_code
  fi
  # Note: the bundled MSBuild crashes hard on linux, so we still rely on the system-installed version
  #export MSBuild=packages/build/RoslynTools.MSBuild/tools/msbuild/MSBuild.exe
  mono packages/build/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx
fi

