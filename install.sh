#!/usr/bin/env bash

if [ "$(id -u)" != "0" ]; then
  echo "This local installation of Paket requires root privileges. Please run script as root (i.e. using 'sudo')." 1>&2
  exit 1
fi

TMP=/tmp/paket/src
LIB=/usr/local/lib
BIN=/usr/local/bin

LATEST=$(curl -s -G -d '$filter=Id%20eq%20'"'"'Paket'"'"'%20and%20IsPrerelease%20eq%20false&$orderby=LastUpdated%20desc&$top=1' "https://www.nuget.org/api/v2/Packages()" | sed -n 's;.*src="\([^"]*\).*;\1;p')

if [ ! "$LATEST" ]; then
  echo "Could not find a paket source on nuget.org. Please check your internet connection and access to https://www.nuget.org."
  exit 1
fi

echo "Installing $LATEST ..."

rm -rf $TMP
mkdir -p $TMP
curl -L $LATEST -o $TMP/paket.zip
unzip -qq -o -d $TMP/ $TMP/paket.zip

rm -rf $LIB/paket
install -d $LIB/paket

for f in $TMP/tools/*; do
  install $f $LIB/paket/
done

rm -rf $BIN/paket

cat >> $BIN/paket <<EOF
#!/usr/bin/env bash
exec mono $LIB/paket/paket.exe "\$@"
EOF

chmod a+x $BIN/paket

echo "Paket installation successful. Type 'paket' for more information'"
exit 0

