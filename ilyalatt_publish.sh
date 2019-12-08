#!/bin/bash
set -e

publish_project() {
  cd src/$1
  rm -rf bin/Release
  dotnet pack -c Release
  dotnet nuget push \
  bin/Release/*.nupkg \
    --source https://api.nuget.org/v3/index.json \
    --api-key $ILYALATT_NUGET_API_KEY
  cd -
}

publish_project "BitTorrentStorage"
publish_project "BitTorrentStorage.Fuse"
