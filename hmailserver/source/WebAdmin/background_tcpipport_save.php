<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != 2)
      hmailHackingAttemp(); // Server admin required

   hmailRequirePostCsrfToken();

   $tcpipportid 	= hmailGetPostVar("tcpipportid",0);
   $protocol	   = hmailGetPostVar("protocol",0);
   $portnumber	   = hmailGetPostVar("portnumber",0);
   $action	      = hmailGetPostVar("action","");
   $ConnectionSecurity	      = hmailGetPostVar("ConnectionSecurity","0");
   $SSLCertificateID	      = hmailGetPostVar("SSLCertificateID","0");
   
   $obSettings   = $obBaseApp->Settings();
   $obTCPIPPorts  = $obSettings->TCPIPPorts;

   if ($action == "edit")
      $obTCPIPPort = $obTCPIPPorts->ItemByDBID($tcpipportid);
   elseif ($action == "add")
      $obTCPIPPort = $obTCPIPPorts->Add();
   elseif ($action == "delete")
   {
      $obTCPIPPorts->DeleteByDBID($tcpipportid);
      header("Location: index.php?page=tcpipports");
      exit();
   }

   $obTCPIPPort->Protocol = $protocol;
   $obTCPIPPort->PortNumber = $portnumber;
   $obTCPIPPort->ConnectionSecurity = $ConnectionSecurity;
   $obTCPIPPort->SSLCertificateID = $SSLCertificateID;
   $obTCPIPPort->Address = hmailGetPostVar("Address","0");
   
   $obTCPIPPort->Save();
   
   $obBaseApp->Stop();
   $obBaseApp->Start();
   
   $tcpipportid = $obTCPIPPort->ID;
   
   header("Location: index.php?page=tcpipport&action=edit&tcpipportid=$tcpipportid");

?>

