#!/usr/bin/sh

ver=$1

if [ -z "$ver" ]; then
   echo "pass version and api-key arguments as follows: publish.sh 1.2.3"
   exit 1
fi

echo $ver

rm -rf artifacts

dotnet build -c Release -o artifacts -p:Version=$ver
dotnet nuget push artifacts/*$ver.nupkg -s https://nuget.pkg.jetbrains.space/finova-ecom/p/main/nuget/v3/index.json --skip-duplicate
# dotnet nuget push artifacts/*$ver.snupkg -k $key -s https://api.nuget.org/v3/index.json --skip-duplicate

rm -rf artifacts
