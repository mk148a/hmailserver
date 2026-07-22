<?php
	if (!defined('IN_WEBADMIN'))
		exit();
      
	if (hmailGetAdminLevel() != ADMIN_SERVER)
		hmailHackingAttemp(); // The user is not server administrator.

	hmailRequirePost();
  
   $TestType = hmailGetPostVar("TestType", "");
   $AntiVirusSettings = $obBaseApp->Settings->AntiVirus;
   
   $result = "";
   $message = "";

   switch ($TestType)
   {
	  case "ClamWin":
		$Executable = hmailGetPostVar("Executable", "");
		$DatabaseFolder = hmailGetPostVar("DatabaseFolder", "");
		$result = $AntiVirusSettings->TestClamWinScanner($Executable, $DatabaseFolder, $message);
		break;
	  case "ClamAV":
		$Hostname = hmailGetPostVar("Hostname", "localhost");
		$Port = hmailResolveLocalScannerPort(hmailGetPostVar("Port", 783));
		$ResolvedHostname = hmailResolveLocalScannerTarget($obBaseApp, $Hostname);
		if ($ResolvedHostname === false || $Port === false)
			hmailRejectScannerTarget();

		$result = $AntiVirusSettings->TestClamAVScanner($ResolvedHostname, $Port, $message);
		break;
	  case "External":
		$Executable = hmailGetPostVar("Executable", "");
		$ReturnValue = hmailGetPostVar("ReturnValue", 0);
		$result = $AntiVirusSettings->TestCustomerScanner($Executable, $ReturnValue, $message);
		break;
      default:
		die;
   }
     
   echo $result;
?>
