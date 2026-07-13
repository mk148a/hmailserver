<?php
   if (!defined('IN_WEBADMIN'))
      exit();
      
   if (hmailGetAdminLevel() != ADMIN_SERVER)
   	hmailHackingAttemp(); // The user is not server administrator.

   hmailRequirePost();
   
   $Hostname = hmailGetVar("Hostname", "localhost");
   $Port = hmailGetVar("Port", 783);
   $ResolvedHostname = hmailResolveLocalScannerTarget($obBaseApp, $Hostname);
   if ($ResolvedHostname === false)
   {
      header("HTTP/1.1 400 Bad Request");
      echo "0";
      die;
   }
   
   $message = "";
   $AntiSpam = $obBaseApp->Settings->AntiSpam;
   $result = $AntiSpam->TestSpamAssassinConnection($ResolvedHostname, $Port, $message);
   
   echo $result;
?>
