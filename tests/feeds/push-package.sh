#! /bin/sh

# pushes a package to each feed
PACKAGE=$1

KLONDIKE_URL=http://localhost:8888
KLONDIKE_ENDPOINT=api/odata
KLONDIKE_KEY=test

SIMPLE_URL=http://localhost:5000
SIMPLE_ENDPOINT=/
SIMPLE_KEY=test

NEXUS_URL=http://localhost:8889
NEXUS_ENDPOINT=repository/nuget-hosted/
NEXUS_KEY=$(curl -s -X POST $NEXUS_URL/service/siesta/rest/v1/script/test/run -H 'Content-Type: text/plain' -u admin:admin123 | jq .result | tr -d '"' )

paket push url $KLONDIKE_URL endpoint $KLONDIKE_ENDPOINT apikey $KLONDIKE_KEY file "$PACKAGE"
paket push url $SIMPLE_URL endpoint $SIMPLE_ENDPOINT apikey $SIMPLE_KEY file "$PACKAGE"
paket push url $NEXUS_URL endpoint $NEXUS_ENDPOINT apikey $NEXUS_KEY file "$PACKAGE"