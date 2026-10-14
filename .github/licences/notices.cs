#!/usr/bin/env dotnet
#:property PublishAot=false
#:include ThirdPartyNotices.cs

Licences.ThirdPartyNotices.Write(args[0], args[1]);
