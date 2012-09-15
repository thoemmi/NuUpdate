param ([int]$port = 8084)

function Get-IISExpress {
	if (Test-Path "c:\program files (x86)\IIS Express\IISExpress.exe") {
		"c:\program files (x86)\IIS Express\IISExpress.exe"
	} elseif (Test-Path "c:\program files\IIS Express\IISExpress.exe") {
		"c:\program files\IIS Express\IISExpress.exe"
	} else {
        throw "IIS Express not found"
	}
}

$websitepath = [IO.Path]::GetFullPath((Join-Path (Split-Path -parent $MyInvocation.MyCommand.path) "..\NuUpdate.NuGetTestServer"))

& (Get-IISExpress) /path:$websitepath /port:$port /trace:i