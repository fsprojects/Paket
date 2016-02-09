#!/bin/bash

json=$(curl https://www.nuget.org/api/v2/package-versions/paket)
version=$(echo $json | tr "," "\n" | tail -1 | sed 's/\]//' | sed 's/\"//g')
currentDir=$(pwd)

mkdir .paket
cd .paket
echo "Downloading https://github.com/fsprojects/Paket/releases/download/$version/paket.bootstrapper.exe"
curl -O -J -L "https://github.com/fsprojects/Paket/releases/download/$version/paket.bootstrapper.exe"
echo "Downloading https://github.com/fsprojects/Paket/releases/download/$version/paket.exe"
curl -O -J -L "https://github.com/fsprojects/Paket/releases/download/$version/paket.exe"
cd ..

mono .paket/paket.exe init

if [ -e ".hgignore" ]; then
  echo ".paket/paket.exe" >> .hgignore
fi

if [ -e ".gitignore" ]; then
  echo ".paket/paket.exe" >> .gitignore
fi

