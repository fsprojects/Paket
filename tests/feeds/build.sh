#! /bin/sh

# make all the packages
cd dummy
dotnet restore
dotnet build
seq 1 100 | xargs -n 1 -P 4 -I %  dotnet pack -o dist /p:PackageVersion=0.0.%
cd ..

# push all the packages to all the feeds
find dummy/dist | xargs -n 1 -I % ./push-package.sh %