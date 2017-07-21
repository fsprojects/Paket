#! /bin/sh

# pushes a package to each feed
PACKAGE=$1

KLONDIKE_URL=http://localhost:8888
KLONDIKE_ENDPOINT=api/odata
KLONDIKE_KEY=998b8711-1794-4c32-8961-21709391b297

SIMPLE_URL=http://localhost:5000
SIMPLE_ENDPOINT=/
SIMPLE_KEY=test

NEXUS_URL=http://localhost:8889
NEXUS_ENDPOINT=repository/nuget-hosted/
NEXUS_KEY=2bf39ea5-c940-39d5-aca2-83c1379e6a36

paket push url $KLONDIKE_URL endpoint $KLONDIKE_ENDPOINT apikey $KLONDIKE_KEY file "$PACKAGE"
paket push url $SIMPLE_URL endpoint $SIMPLE_ENDPOINT apikey $SIMPLE_KEY file "$PACKAGE"
paket push url $NEXUS_URL endpoint $NEXUS_ENDPOINT apikey $NEXUS_KEY file "$PACKAGE"