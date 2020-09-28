# SAS Enterprise Guide code exporter  [![Build Status](https://travis-ci.org/joonasmaunu/sasegexport.svg?branch=master)](https://travis-ci.org/joonasmaunu/sasegexport.svg?branch=master)

SAS Enterprise Guide code exporter exports SAS code from SAS Enterprise Guide processes.

# Requirements

 - dotnet: 3.1

# Build

 - dotnet restore
 - dotnet build --configuration Release --no-restore

# Usage

From command prompt:

EGExport.exe "/path/to/yuor/SAS/project.egp"

This returns the SAS code as output.