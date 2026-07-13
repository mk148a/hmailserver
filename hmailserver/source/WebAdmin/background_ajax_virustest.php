<?php
	if (!defined('IN_WEBADMIN'))
		exit();
      
	if (hmailGetAdminLevel() != ADMIN_SERVER)
		hmailHackingAttemp(); // The user is not server administrator.

	hmailRequirePost();
  
   $TestType = hmailGetVar("TestType", "");
   $AntiVirusSettings = $obBaseApp->Settings->AntiVirus;
   
   $result = "";
   $message = "";

   switch ($TestType)
   {
	  case "ClamWin":
		$Executable = hmailGetVar("Executable", "");
		$DatabaseFolder = hmailGetVar("DatabaseFolder", "");
		$result = $AntiVirusSettings->TestClamWinScanner($Executable, $DatabaseFolder, $message);
		break;
	  case "ClamAV":
		$Hostname = hmailGetVar("Hostname", "localhost");
		$Port = hmailGetVar("Port", 783);
		$ResolvedHostname = hmailResolveLocalScannerTarget($obBaseApp, $Hostname);
		if ($ResolvedHostname === false)
		{
			header("HTTP/1.1 400 Bad Request");
			echo "0";
			die;
		}

		$result = $AntiVirusSettings->TestClamAVScanner($ResolvedHostname, $Port, $message);
		break;
	  case "External":
		$Executable = hmailGetVar("Executable", "");
		$ReturnValue = hmailGetVar("ReturnValue", 0);
		$result = $AntiVirusSettings->TestCustomerScanner($Executable, $ReturnValue, $message);
		break;
      default:
		die;
   }
     
   echo $result;
?>
