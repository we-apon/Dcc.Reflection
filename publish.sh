#!/usr/bin/sh

ver=$1
key=$2

if [ -z "$ver" ]; then
   echo "pass version and api-key arguments as follows: publish.sh 1.2.3 apikey"
   exit 1
fi

if [ -z "$key" ]; then
   echo "pass version and api-key arguments as follows: publish.sh 1.2.3 apikey"
   exit 1
fi

echo $ver
echo $key

rm -rf artifacts

dotnet build -o artifacts -p:Version=$ver
dotnet nuget push artifacts/*$ver.nupkg -k $key -s https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push artifacts/*$ver.snupkg -k $key -s https://api.nuget.org/v3/index.json --skip-duplicate

