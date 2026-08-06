<?php
   if (!defined('IN_WEBADMIN'))
      exit();
      
   if (hmailGetAdminLevel() != ADMIN_SERVER)
   	hmailHackingAttemp(); // The user is not server administrator.

   hmailRequirePostCsrfToken();
   
   $Hostname = hmailGetPostVar("Hostname", "localhost");
   $Port = hmailResolveLocalScannerPort(hmailGetPostVar("Port", 783));
   $ResolvedHostname = hmailResolveLocalScannerTarget($obBaseApp, $Hostname);
   if ($ResolvedHostname === false || $Port === false)
      hmailRejectScannerTarget();
   
   $message = "";
   $AntiSpam = $obBaseApp->Settings->AntiSpam;
   $result = $AntiSpam->TestSpamAssassinConnection($ResolvedHostname, $Port, $message);
   
   echo $result;
?>
