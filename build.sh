#!/bin/bash

dotnet tool restore
dotnet paket restore
dotnet build src/Aardvark.Media.NoExamples.NonWindows.slnf