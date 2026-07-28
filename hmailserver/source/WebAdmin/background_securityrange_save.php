<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != ADMIN_SERVER)
   	hmailHackingAttemp(); // The user is not server administrator.

   hmailRequirePostCsrfToken();
   
   $action	            = hmailGetPostVar("action","");
   $securityrangeid	   = hmailGetPostVar("securityrangeid","");
   
   if ($action == "edit")
      $obSecurityRange     = $obBaseApp->Settings->SecurityRanges->ItemByDBID($securityrangeid);
   elseif ($action == "add")
      $obSecurityRange     = $obBaseApp->Settings->SecurityRanges->Add();
   elseif ($action == "delete")
   {
      $obBaseApp->Settings->SecurityRanges->DeleteByDBID($securityrangeid);
      header("Location: index.php?page=securityranges");
   }
      
   // Fetch form
   $securityrangename		= hmailGetPostVar("securityrangename","");
   $securityrangepriority	= hmailGetPostVar("securityrangepriority","0");
   $securityrangelowerip	= hmailGetPostVar("securityrangelowerip","0");
   $securityrangeupperip	= hmailGetPostVar("securityrangeupperip","0");
   
   $allowsmtpconnections	= hmailGetPostVar("allowsmtpconnections","0");
   $allowpop3connections	= hmailGetPostVar("allowpop3connections","0");
   $allowimapconnections	= hmailGetPostVar("allowimapconnections","0");
   
   $allowlocaltolocal		= hmailGetPostVar("allowlocaltolocal","0");
   $allowlocaltoremote		= hmailGetPostVar("allowlocaltoremote","0");
   $allowremotetolocal		= hmailGetPostVar("allowremotetolocal","0");
   $allowremotetoremote		= hmailGetPostVar("allowremotetoremote","0");

   $enablespamprotection	= hmailGetPostVar("enablespamprotection","0");
   $EnableAntiVirus         = hmailGetPostVar("EnableAntiVirus","0");
   
   $IsForwardingRelay	   = hmailGetPostVar("IsForwardingRelay","0");
   $RequireSSLTLSForAuth   = hmailGetPostVar("RequireSSLTLSForAuth","0");
   
   $Expires	   = hmailGetPostVar("Expires",0);
   $ExpiresTime	   = hmailGetPostVar("ExpiresTime","");
   
   // Save the changes
   $obSecurityRange->Name = $securityrangename;
   $obSecurityRange->Priority = $securityrangepriority;
   $obSecurityRange->LowerIP = $securityrangelowerip;
   $obSecurityRange->UpperIP = $securityrangeupperip;
   
   $obSecurityRange->AllowSMTPConnections = $allowsmtpconnections;
   $obSecurityRange->AllowPOP3Connections = $allowpop3connections;
   $obSecurityRange->AllowIMAPConnections = $allowimapconnections;
   
   $obSecurityRange->AllowDeliveryFromLocalToLocal = $allowlocaltolocal;
   $obSecurityRange->AllowDeliveryFromLocalToRemote = $allowlocaltoremote;
   $obSecurityRange->AllowDeliveryFromRemoteToLocal = $allowremotetolocal;
   $obSecurityRange->AllowDeliveryFromRemoteToRemote = $allowremotetoremote;

   $obSecurityRange->RequireSMTPAuthLocalToLocal = hmailGetPostVar("RequireSMTPAuthLocalToLocal", 0);
   $obSecurityRange->RequireSMTPAuthLocalToExternal = hmailGetPostVar("RequireSMTPAuthLocalToExternal", 0);
   $obSecurityRange->RequireSMTPAuthExternalToLocal = hmailGetPostVar("RequireSMTPAuthExternalToLocal", 0);
   $obSecurityRange->RequireSMTPAuthExternalToExternal = hmailGetPostVar("RequireSMTPAuthExternalToExternal", 0);

   $obSecurityRange->EnableSpamProtection = $enablespamprotection;
   $obSecurityRange->EnableAntiVirus = $EnableAntiVirus;
   $obSecurityRange->IsForwardingRelay = $IsForwardingRelay;
   $obSecurityRange->RequireSSLTLSForAuth = $RequireSSLTLSForAuth;
   
   $obSecurityRange->Expires = $Expires;
   $obSecurityRange->ExpiresTime = $ExpiresTime;

   $obSecurityRange->Save();
   
   $securityrangeid = $obSecurityRange->ID;
   
   header("Location: index.php?page=securityrange&action=edit&securityrangeid=$securityrangeid");
?>

