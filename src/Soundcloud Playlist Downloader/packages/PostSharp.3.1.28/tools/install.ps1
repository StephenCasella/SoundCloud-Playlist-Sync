param($installPath, $toolsPath, $package, $project)


function PathToUri([string] $path)
{
    return new-object Uri('file://' + $path.Replace("%","%25").Replace("#","%23").Replace("$","%24").Replace("+","%2B").Replace(",","%2C").Replace("=","%3D").Replace("@","%40").Replace("~","%7E").Replace("^","%5E"))
}

function UriToPath([System.Uri] $uri)
{
    return [System.Uri]::UnescapeDataString( $uri.ToString() ).Replace([System.IO.Path]::AltDirectorySeparatorChar, [System.IO.Path]::DirectorySeparatorChar)
}

$targetsFile = [System.IO.Path]::Combine($toolsPath, 'PostSharp.targets')

# Need to load MSBuild assembly if it's not loaded yet.
Add-Type -AssemblyName 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

# Grab the loaded MSBuild project for the project
$msbuild = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName) | Select-Object -First 1

# Make the path to the targets file relative.
$projectUri = PathToUri $project.FullName
$targetUri = PathToUri $targetsFile

$relativePath = UriToPath $projectUri.MakeRelativeUri($targetUri)

# Remove elements from previous installations or versions.
$itemsToRemove = @()
$itemsToRemove += $msbuild.Xml.Properties | Where-Object {$_.Name.ToLowerInvariant() -eq "dontimportpostsharp" }
$itemsToRemove += $msbuild.Xml.Properties | Where-Object {$_.Name.ToLowerInvariant().EndsWith("postsharpignoredpackages") }
$itemsToRemove += $msbuild.Xml.Imports | Where-Object {$_.Project.ToLowerInvariant().EndsWith("postsharp.targets") } 
$itemsToRemove += $msbuild.Xml.Targets | Where-Object {$_.Name.ToLowerInvariant() -eq "ensurepostsharpimported" }
$itemsToRemove += $project.Object.References | Where-Object {$_.Identity.ToLowerInvariant().StartsWith("postsharp.public") } 
$itemsToRemove += $project.Object.References | Where-Object {$_.Identity.ToLowerInvariant().StartsWith("postsharp.laos") } 
$itemsToRemove += $msbuild.Xml.Targets | Where-Object {$_.Name.ToLowerInvariant() -eq "ensurepostsharpimported" }

if ($itemsToRemove -and $itemsToRemove.length)
{
    foreach ($itemToRemove in $itemsToRemove)
    {
        $msbuild.Xml.RemoveChild($itemToRemove) | out-null
    }
}

# Set property DontImportPostSharp to prevent locally-installed previous versions of PostSharp to interfere.
$msbuild.Xml.AddProperty( "DontImportPostSharp", "True" ) | Out-Null

# Add import to PostSharp.targets
$import = $msbuild.Xml.AddImport($relativePath)
$import.set_Condition( "Exists('$relativePath')" ) | Out-Null
[string]::Format("Added import of '{0}'.", $relativePath )

 # Add a target to fail the build when our targets are not imported
$target = $msbuild.Xml.AddTarget("EnsurePostSharpImported")
$target.BeforeTargets = "BeforeBuild"
$target.Condition = "'`$(PostSharp30Imported)' == ''"

# if the targets don't exist at the time the target runs, package restore didn't run
$errorTask = $target.AddTask("Error")
$errorTask.Condition = "!Exists('$relativePath')"
$errorTask.SetParameter("Text", "This project references NuGet package(s) that are missing on this computer. Enable NuGet Package Restore to download them.  For more information, see http://www.postsharp.net/links/nuget-restore.");

# if the targets exist at the time the target runs, package restore ran but the build didn't import the targets.
$errorTask = $target.AddTask("Error")
$errorTask.Condition = "Exists('$relativePath')"
$errorTask.SetParameter("Text", "The build restored NuGet packages. Build the project again to include these packages in the build. For more information, see http://www.postsharp.net/links/nuget-restore.");

$project.Save()
$project.Object.Refresh()

# Asynchronously run setup wizard if necessary. Since the setup wizard is compressed in PostSharp-Tools.exe, the easiest is to run it through MSBuild.
$msbuildExe = [System.IO.Path]::Combine( [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory(), "msbuild.exe")
"Starting $msbuildExe"
Start-Process -FilePath $msbuildExe -ArgumentList @("""$toolsPath\PostSharp.targets""", "/t:PostSharp30InstallVsx /p:BuildingInsideVisualStudio=True") -WindowStyle Hidden
	
# SIG # Begin signature block
# MIId/AYJKoZIhvcNAQcCoIId7TCCHekCAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUO//ssc5LpSyEsxuLHBNm3LCO
# McGgghjsMIID7jCCA1egAwIBAgIQfpPr+3zGTlnqS5p31Ab8OzANBgkqhkiG9w0B
# AQUFADCBizELMAkGA1UEBhMCWkExFTATBgNVBAgTDFdlc3Rlcm4gQ2FwZTEUMBIG
# A1UEBxMLRHVyYmFudmlsbGUxDzANBgNVBAoTBlRoYXd0ZTEdMBsGA1UECxMUVGhh
# d3RlIENlcnRpZmljYXRpb24xHzAdBgNVBAMTFlRoYXd0ZSBUaW1lc3RhbXBpbmcg
# Q0EwHhcNMTIxMjIxMDAwMDAwWhcNMjAxMjMwMjM1OTU5WjBeMQswCQYDVQQGEwJV
# UzEdMBsGA1UEChMUU3ltYW50ZWMgQ29ycG9yYXRpb24xMDAuBgNVBAMTJ1N5bWFu
# dGVjIFRpbWUgU3RhbXBpbmcgU2VydmljZXMgQ0EgLSBHMjCCASIwDQYJKoZIhvcN
# AQEBBQADggEPADCCAQoCggEBALGss0lUS5ccEgrYJXmRIlcqb9y4JsRDc2vCvy5Q
# WvsUwnaOQwElQ7Sh4kX06Ld7w3TMIte0lAAC903tv7S3RCRrzV9FO9FEzkMScxeC
# i2m0K8uZHqxyGyZNcR+xMd37UWECU6aq9UksBXhFpS+JzueZ5/6M4lc/PcaS3Er4
# ezPkeQr78HWIQZz/xQNRmarXbJ+TaYdlKYOFwmAUxMjJOxTawIHwHw103pIiq8r3
# +3R8J+b3Sht/p8OeLa6K6qbmqicWfWH3mHERvOJQoUvlXfrlDqcsn6plINPYlujI
# fKVOSET/GeJEB5IL12iEgF1qeGRFzWBGflTBE3zFefHJwXECAwEAAaOB+jCB9zAd
# BgNVHQ4EFgQUX5r1blzMzHSa1N197z/b7EyALt0wMgYIKwYBBQUHAQEEJjAkMCIG
# CCsGAQUFBzABhhZodHRwOi8vb2NzcC50aGF3dGUuY29tMBIGA1UdEwEB/wQIMAYB
# Af8CAQAwPwYDVR0fBDgwNjA0oDKgMIYuaHR0cDovL2NybC50aGF3dGUuY29tL1Ro
# YXd0ZVRpbWVzdGFtcGluZ0NBLmNybDATBgNVHSUEDDAKBggrBgEFBQcDCDAOBgNV
# HQ8BAf8EBAMCAQYwKAYDVR0RBCEwH6QdMBsxGTAXBgNVBAMTEFRpbWVTdGFtcC0y
# MDQ4LTEwDQYJKoZIhvcNAQEFBQADgYEAAwmbj3nvf1kwqu9otfrjCR27T4IGXTdf
# plKfFo3qHJIJRG71betYfDDo+WmNI3MLEm9Hqa45EfgqsZuwGsOO61mWAK3ODE2y
# 0DGmCFwqevzieh1XTKhlGOl5QGIllm7HxzdqgyEIjkHq3dlXPx13SYcqFgZepjhq
# IhKjURmDfrYwggSjMIIDi6ADAgECAhAOz/Q4yP6/NW4E2GqYGxpQMA0GCSqGSIb3
# DQEBBQUAMF4xCzAJBgNVBAYTAlVTMR0wGwYDVQQKExRTeW1hbnRlYyBDb3Jwb3Jh
# dGlvbjEwMC4GA1UEAxMnU3ltYW50ZWMgVGltZSBTdGFtcGluZyBTZXJ2aWNlcyBD
# QSAtIEcyMB4XDTEyMTAxODAwMDAwMFoXDTIwMTIyOTIzNTk1OVowYjELMAkGA1UE
# BhMCVVMxHTAbBgNVBAoTFFN5bWFudGVjIENvcnBvcmF0aW9uMTQwMgYDVQQDEytT
# eW1hbnRlYyBUaW1lIFN0YW1waW5nIFNlcnZpY2VzIFNpZ25lciAtIEc0MIIBIjAN
# BgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAomMLOUS4uyOnREm7Dv+h8GEKU5Ow
# mNutLA9KxW7/hjxTVQ8VzgQ/K/2plpbZvmF5C1vJTIZ25eBDSyKV7sIrQ8Gf2Gi0
# jkBP7oU4uRHFI/JkWPAVMm9OV6GuiKQC1yoezUvh3WPVF4kyW7BemVqonShQDhfu
# ltthO0VRHc8SVguSR/yrrvZmPUescHLnkudfzRC5xINklBm9JYDh6NIipdC6Anqh
# d5NbZcPuF3S8QYYq3AhMjJKMkS2ed0QfaNaodHfbDlsyi1aLM73ZY8hJnTrFxeoz
# C9Lxoxv0i77Zs1eLO94Ep3oisiSuLsdwxb5OgyYI+wu9qU+ZCOEQKHKqzQIDAQAB
# o4IBVzCCAVMwDAYDVR0TAQH/BAIwADAWBgNVHSUBAf8EDDAKBggrBgEFBQcDCDAO
# BgNVHQ8BAf8EBAMCB4AwcwYIKwYBBQUHAQEEZzBlMCoGCCsGAQUFBzABhh5odHRw
# Oi8vdHMtb2NzcC53cy5zeW1hbnRlYy5jb20wNwYIKwYBBQUHMAKGK2h0dHA6Ly90
# cy1haWEud3Muc3ltYW50ZWMuY29tL3Rzcy1jYS1nMi5jZXIwPAYDVR0fBDUwMzAx
# oC+gLYYraHR0cDovL3RzLWNybC53cy5zeW1hbnRlYy5jb20vdHNzLWNhLWcyLmNy
# bDAoBgNVHREEITAfpB0wGzEZMBcGA1UEAxMQVGltZVN0YW1wLTIwNDgtMjAdBgNV
# HQ4EFgQURsZpow5KFB7VTNpSYxc/Xja8DeYwHwYDVR0jBBgwFoAUX5r1blzMzHSa
# 1N197z/b7EyALt0wDQYJKoZIhvcNAQEFBQADggEBAHg7tJEqAEzwj2IwN3ijhCcH
# bxiy3iXcoNSUA6qGTiWfmkADHN3O43nLIWgG2rYytG2/9CwmYzPkSWRtDebDZw73
# BaQ1bHyJFsbpst+y6d0gxnEPzZV03LZc3r03H0N45ni1zSgEIKOq8UvEiCmRDoDR
# EfzdXHZuT14ORUZBbg2w6jiasTraCXEQ/Bx5tIB7rGn0/Zy2DBYr8X9bCT2bW+IW
# yhOBbQAuOA2oKY8s4bL0WqkBrxWcLC9JG9siu8P+eJRRw4axgohd8D20UaF5Mysu
# e7ncIAkTcetqGVvP6KUwVyyJST+5z3/Jvz4iaGNTmr1pdKzFHTx/kuDDvBzYBHUw
# ggTTMIIDu6ADAgECAhAY2tGeJn3ou0ohWM3MaztKMA0GCSqGSIb3DQEBBQUAMIHK
# MQswCQYDVQQGEwJVUzEXMBUGA1UEChMOVmVyaVNpZ24sIEluYy4xHzAdBgNVBAsT
# FlZlcmlTaWduIFRydXN0IE5ldHdvcmsxOjA4BgNVBAsTMShjKSAyMDA2IFZlcmlT
# aWduLCBJbmMuIC0gRm9yIGF1dGhvcml6ZWQgdXNlIG9ubHkxRTBDBgNVBAMTPFZl
# cmlTaWduIENsYXNzIDMgUHVibGljIFByaW1hcnkgQ2VydGlmaWNhdGlvbiBBdXRo
# b3JpdHkgLSBHNTAeFw0wNjExMDgwMDAwMDBaFw0zNjA3MTYyMzU5NTlaMIHKMQsw
# CQYDVQQGEwJVUzEXMBUGA1UEChMOVmVyaVNpZ24sIEluYy4xHzAdBgNVBAsTFlZl
# cmlTaWduIFRydXN0IE5ldHdvcmsxOjA4BgNVBAsTMShjKSAyMDA2IFZlcmlTaWdu
# LCBJbmMuIC0gRm9yIGF1dGhvcml6ZWQgdXNlIG9ubHkxRTBDBgNVBAMTPFZlcmlT
# aWduIENsYXNzIDMgUHVibGljIFByaW1hcnkgQ2VydGlmaWNhdGlvbiBBdXRob3Jp
# dHkgLSBHNTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAK8kCAgpejWe
# YAyq50s7Ttx8vDxFHLsr4P4pAvlXCKNkhRUn9fGtyDGJXSLoKqqmQrOP+LlVt7G3
# S7P+j34HV+zvQ9tmYhVhz2ANpNje+ODDYgg9VBPrScpZVIUm5SuPG5/r9aGRwjNJ
# 2ENjalJL0o/ocFFN0Ylpe8dw9rPcEnTbe11LVtOWvxV3obD0oiXyrxySZxjl9AYE
# 75C55ADk3Tq1Gf8CuvQ87uCL6zeL7PTXrPL28D2v3XWRMxkdHEDLdCQZIZPZFP6s
# KlLHj9UESeSNY0eIPGmDy/5HvSt+T8WVrg6d1NFDwGdz4xQIfuU/n3O4MwrPXT80
# h5aK7lPoJRUCAwEAAaOBsjCBrzAPBgNVHRMBAf8EBTADAQH/MA4GA1UdDwEB/wQE
# AwIBBjBtBggrBgEFBQcBDARhMF+hXaBbMFkwVzBVFglpbWFnZS9naWYwITAfMAcG
# BSsOAwIaBBSP5dMahqyNjmvDz4Bq1EgYLHsZLjAlFiNodHRwOi8vbG9nby52ZXJp
# c2lnbi5jb20vdnNsb2dvLmdpZjAdBgNVHQ4EFgQUf9Nlp8Ld7LvwMAnzQzn6Aq8z
# MTMwDQYJKoZIhvcNAQEFBQADggEBAJMkSjBfYs/YGpgvPercmS29d/aleSI47MSn
# oHgSrWIORXBkxeeXZi2YCX5fr9bMKGXyAaoIGkfe+fl8kloIaSAN2T5tbjwNbtjm
# BpFAGLn4we3f20Gq4JYgyc1kFTiByZTuooQpCxNvjtsM3SUC26SLGUTSQXoFaUpY
# T2DKfoJqCwKqJRc5tdt/54RlKpWKvYbeXoEWgy0QzN79qIIqbSgfDQvE5ecaJhnh
# 9BFvELWV/OdCBTLbzp1RXii2noXTW++lfUVAco63DmsOBvszNUhxuJ0ni8RlXw2G
# dpxEevaVXPZdMggzpFS2GD9oXPJCSoU4VINf0egs8qwR1qjtY2owggVqMIIEUqAD
# AgECAhAMtnr7s7inKYYI4A6UzzU+MA0GCSqGSIb3DQEBBQUAMIG0MQswCQYDVQQG
# EwJVUzEXMBUGA1UEChMOVmVyaVNpZ24sIEluYy4xHzAdBgNVBAsTFlZlcmlTaWdu
# IFRydXN0IE5ldHdvcmsxOzA5BgNVBAsTMlRlcm1zIG9mIHVzZSBhdCBodHRwczov
# L3d3dy52ZXJpc2lnbi5jb20vcnBhIChjKTEwMS4wLAYDVQQDEyVWZXJpU2lnbiBD
# bGFzcyAzIENvZGUgU2lnbmluZyAyMDEwIENBMB4XDTExMDYyNDAwMDAwMFoXDTE0
# MDcwMzIzNTk1OVowga0xCzAJBgNVBAYTAkNaMQ8wDQYDVQQIEwZQcmFndWUxDzAN
# BgNVBAcTBlByYWd1ZTEdMBsGA1UEChQUU2hhcnBDcmFmdGVycyBzLnIuby4xPjA8
# BgNVBAsTNURpZ2l0YWwgSUQgQ2xhc3MgMyAtIE1pY3Jvc29mdCBTb2Z0d2FyZSBW
# YWxpZGF0aW9uIHYyMR0wGwYDVQQDFBRTaGFycENyYWZ0ZXJzIHMuci5vLjCCASIw
# DQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAK0A6khiBRSzjcckXo8lVbY/TcZH
# K/LT5Gwkg7EyyiBivHnIRsuVNazEu8ka9ZmVHNH+8V0vTlRu9irToZVkOW0TqVc0
# KJxDa9Om32J5aauQ0VWBI13EI4Rzx7x+X6lZv65w7N11bdHjSwOdVZqNIwAH6YvF
# gUa1j8MOKYKTcPUb7Pr6slRRqvWNJaSzc5sKDo2u3ztoM6Mi8ZOLTvlIi2WAu9AO
# JrbJLnIS/SPbgL+A3s1Mt/GquVs3vocaHLg3/6Aol4XKmI/YFcOASD72b9ZfpVTg
# hd61qnQ9IXwtjHCDAkMHIxnyk/hUfXN2W+5EFt0AvGN7j+AY8wW8yoJM5o8CAwEA
# AaOCAXswggF3MAkGA1UdEwQCMAAwDgYDVR0PAQH/BAQDAgeAMEAGA1UdHwQ5MDcw
# NaAzoDGGL2h0dHA6Ly9jc2MzLTIwMTAtY3JsLnZlcmlzaWduLmNvbS9DU0MzLTIw
# MTAuY3JsMEQGA1UdIAQ9MDswOQYLYIZIAYb4RQEHFwMwKjAoBggrBgEFBQcCARYc
# aHR0cHM6Ly93d3cudmVyaXNpZ24uY29tL3JwYTATBgNVHSUEDDAKBggrBgEFBQcD
# AzBxBggrBgEFBQcBAQRlMGMwJAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3NwLnZlcmlz
# aWduLmNvbTA7BggrBgEFBQcwAoYvaHR0cDovL2NzYzMtMjAxMC1haWEudmVyaXNp
# Z24uY29tL0NTQzMtMjAxMC5jZXIwHwYDVR0jBBgwFoAUz5mp6nsm9EvJjo/X8AUm
# 7+PSp50wEQYJYIZIAYb4QgEBBAQDAgQQMBYGCisGAQQBgjcCARsECDAGAQEAAQH/
# MA0GCSqGSIb3DQEBBQUAA4IBAQBy6KC5DX/wo55ppitx2h/f8UxWmNsHvvArhQKQ
# zSfYtQYQ8fueV3FYaC/9vkd1Ard35A3AsT1UpLueZ4NVXA4ltqKeXM5F6e+hPWe5
# nc5zkDr8WFrLqjsPjXc9HiHpoQm+yZ9Lnc/wkA+eHvrLp+Dml5XJWOvbzHf9vWLz
# SiEaqI2miA6yzNcSZmALzaner1j3AjaMU8Omr0UvsIpJwoJVDFWopNzCJG7ovJwh
# ajBy1mthHS/l1pOoWa2D/GffHmseDgltdlwjVZ3EnUQ4HN8cZ7vSB/re1hzPskC5
# QLajNvHRjOsnjr4ZfGWZmDq3ZQ2E4mTyYXlIWFI4Yhh9G9XuMIIGCjCCBPKgAwIB
# AgIQUgDlqiVW/BqG7ZbJ1EszxzANBgkqhkiG9w0BAQUFADCByjELMAkGA1UEBhMC
# VVMxFzAVBgNVBAoTDlZlcmlTaWduLCBJbmMuMR8wHQYDVQQLExZWZXJpU2lnbiBU
# cnVzdCBOZXR3b3JrMTowOAYDVQQLEzEoYykgMjAwNiBWZXJpU2lnbiwgSW5jLiAt
# IEZvciBhdXRob3JpemVkIHVzZSBvbmx5MUUwQwYDVQQDEzxWZXJpU2lnbiBDbGFz
# cyAzIFB1YmxpYyBQcmltYXJ5IENlcnRpZmljYXRpb24gQXV0aG9yaXR5IC0gRzUw
# HhcNMTAwMjA4MDAwMDAwWhcNMjAwMjA3MjM1OTU5WjCBtDELMAkGA1UEBhMCVVMx
# FzAVBgNVBAoTDlZlcmlTaWduLCBJbmMuMR8wHQYDVQQLExZWZXJpU2lnbiBUcnVz
# dCBOZXR3b3JrMTswOQYDVQQLEzJUZXJtcyBvZiB1c2UgYXQgaHR0cHM6Ly93d3cu
# dmVyaXNpZ24uY29tL3JwYSAoYykxMDEuMCwGA1UEAxMlVmVyaVNpZ24gQ2xhc3Mg
# MyBDb2RlIFNpZ25pbmcgMjAxMCBDQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCC
# AQoCggEBAPUjS16l14q7MunUV/fv5Mcmfq0ZmP6onX2U9jZrENd1gTB/BGh/yyt1
# Hs0dCIzfaZSnN6Oce4DgmeHuN01fzjsU7obU0PUnNbwlCzinjGOdF6MIpauw+81q
# YoJM1SHaG9nx44Q7iipPhVuQAU/Jp3YQfycDfL6ufn3B3fkFvBtInGnnwKQ8PEEA
# Pt+W5cXklHHWVQHHACZKQDy1oSapDKdtgI6QJXvPvz8c6y+W+uWHd8a1VrJ6O1Qw
# UxvfYjT/HtH0WpMoheVMF05+W/2kk5l/383vpHXv7xX2R+f4GXLYLjQaprSnTH69
# u08MPVfxMNamNo7WgHbXGS6lzX40LYkCAwEAAaOCAf4wggH6MBIGA1UdEwEB/wQI
# MAYBAf8CAQAwcAYDVR0gBGkwZzBlBgtghkgBhvhFAQcXAzBWMCgGCCsGAQUFBwIB
# FhxodHRwczovL3d3dy52ZXJpc2lnbi5jb20vY3BzMCoGCCsGAQUFBwICMB4aHGh0
# dHBzOi8vd3d3LnZlcmlzaWduLmNvbS9ycGEwDgYDVR0PAQH/BAQDAgEGMG0GCCsG
# AQUFBwEMBGEwX6FdoFswWTBXMFUWCWltYWdlL2dpZjAhMB8wBwYFKw4DAhoEFI/l
# 0xqGrI2Oa8PPgGrUSBgsexkuMCUWI2h0dHA6Ly9sb2dvLnZlcmlzaWduLmNvbS92
# c2xvZ28uZ2lmMDQGA1UdHwQtMCswKaAnoCWGI2h0dHA6Ly9jcmwudmVyaXNpZ24u
# Y29tL3BjYTMtZzUuY3JsMDQGCCsGAQUFBwEBBCgwJjAkBggrBgEFBQcwAYYYaHR0
# cDovL29jc3AudmVyaXNpZ24uY29tMB0GA1UdJQQWMBQGCCsGAQUFBwMCBggrBgEF
# BQcDAzAoBgNVHREEITAfpB0wGzEZMBcGA1UEAxMQVmVyaVNpZ25NUEtJLTItODAd
# BgNVHQ4EFgQUz5mp6nsm9EvJjo/X8AUm7+PSp50wHwYDVR0jBBgwFoAUf9Nlp8Ld
# 7LvwMAnzQzn6Aq8zMTMwDQYJKoZIhvcNAQEFBQADggEBAFYi5jSkxGHLSLkBrVao
# ZA/ZjJHEu8wM5a16oCJ/30c4Si1s0X9xGnzscKmx8E/kDwxT+hVe/nSYSSSFgSYc
# kRRHsExjjLuhNNTGRegNhSZzA9CpjGRt3HGS5kUFYBVZUTn8WBRr/tSk7XlrCAxB
# cuc3IgYJviPpP0SaHulhncyxkFz8PdKNrEI9ZTbUtD1AKI+bEM8jJsxLIMuQH12M
# TDTKPNjlN9ZvpSC9NOsm2a4N58Wa96G0IZEzb4boWLslfHQOWP51G2M/zjF8m48b
# lp7FU3aEW5ytkfqs7ZO6XcghU8KCU2OvEg1QhxEbPVRSloosnD2SGgiaBS7Hk6VI
# kdMxggR6MIIEdgIBATCByTCBtDELMAkGA1UEBhMCVVMxFzAVBgNVBAoTDlZlcmlT
# aWduLCBJbmMuMR8wHQYDVQQLExZWZXJpU2lnbiBUcnVzdCBOZXR3b3JrMTswOQYD
# VQQLEzJUZXJtcyBvZiB1c2UgYXQgaHR0cHM6Ly93d3cudmVyaXNpZ24uY29tL3Jw
# YSAoYykxMDEuMCwGA1UEAxMlVmVyaVNpZ24gQ2xhc3MgMyBDb2RlIFNpZ25pbmcg
# MjAxMCBDQQIQDLZ6+7O4pymGCOAOlM81PjAJBgUrDgMCGgUAoHgwGAYKKwYBBAGC
# NwIBDDEKMAigAoAAoQKAADAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgor
# BgEEAYI3AgELMQ4wDAYKKwYBBAGCNwIBFTAjBgkqhkiG9w0BCQQxFgQUk51/HNXO
# wIDJhsxApDyv6Ne9K5gwDQYJKoZIhvcNAQEBBQAEggEATtMojnzLWW1Wn06aNor5
# vSEDUSo6BY3x8S38imYkHccVVjvTEJkFrS+gjlEP1McaUZO8Th6FDg22nyrEqAR/
# DvL9C0Bj547YPjubdE0C+KUizVVQxFnvPyBhNHFezwFSjCBJp8ECNNsG/4EXRgxq
# 7yFp721kYlWRSijT2u0ZdZD5xw+rpyj+gIm82NH0bTG1CeQ79hcpiDmCMUldxgkV
# YRVHoyCj5YAMmVkfUNOfgrNheB7Td1m1K9m+7FWgzLkNYIQ8t3g/vThrs7NPvkkQ
# rFWPyJsTETlL0y7PCsk2dWBLEAGrUnRnJFPi0VGh9lmCd+LBlAAVPJCtFPzdwUGW
# MKGCAgswggIHBgkqhkiG9w0BCQYxggH4MIIB9AIBATByMF4xCzAJBgNVBAYTAlVT
# MR0wGwYDVQQKExRTeW1hbnRlYyBDb3Jwb3JhdGlvbjEwMC4GA1UEAxMnU3ltYW50
# ZWMgVGltZSBTdGFtcGluZyBTZXJ2aWNlcyBDQSAtIEcyAhAOz/Q4yP6/NW4E2GqY
# GxpQMAkGBSsOAwIaBQCgXTAYBgkqhkiG9w0BCQMxCwYJKoZIhvcNAQcBMBwGCSqG
# SIb3DQEJBTEPFw0xNDAxMDcxNTAyNTRaMCMGCSqGSIb3DQEJBDEWBBR1STbRZhg4
# 9+2yQuAc1iNe6amcUzANBgkqhkiG9w0BAQEFAASCAQB9zyt1Af1Dj5MGXYFGAFuN
# cBDD8Y7NXVw2R/ktlbyt3BSgsNDFUk8QYCZRGTh6Y0zIkDYug1b5gK3D19kY54Ic
# p+CM5hVdeX4pjW2omneLQ36Dod8vLV7TqLaHPgnA++0VqI32QmSplKPSjtSCX38C
# Vfi1hvUuIOCdWdsNFicMWWWhj0FLNW+DJcv2SOLQonqDXKJ4He1s/nH/pp91O0/O
# i5qVCrqfQ4H3xxVElB+/m+DPCBmHBD5We4wLtp28sbXZwJl53eCCI8TyXpGL0OJ6
# 0Q61Dw6lxaa45kLWuF0UpyDJRRBJ3TBFZX5IxIYnYG21VQ57AP7l3hOMTc0XMSm6
# SIG # End signature block
