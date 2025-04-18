#!/bin/bash

this_path=$(readlink -f $0)
this_dir=$(dirname $this_path)

dotnet nuget push $this_dir/SourceGenerator/bin/Release/Std.TextTemplating.SourceGenerator.1.0.9.nupkg --api-key $1 --source https://api.nuget.org/v3/index.json
