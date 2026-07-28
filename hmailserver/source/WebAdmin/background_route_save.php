<?php

   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != 2)
	hmailHackingAttemp(); // Domain admin but not for this domain.

   hmailRequirePostCsrfToken();
   
   $action	            = hmailGetPostVar("action","");
   $routeid	   = hmailGetPostVar("routeid","");
   
   if ($action == "edit")
      $obRoute     = $obBaseApp->Settings->Routes->ItemByDBID($routeid);
   elseif ($action == "add")
      $obRoute    = $obBaseApp->Settings->Routes->Add();
   elseif ($action == "delete")
   {
      $obBaseApp->Settings->Routes->DeleteByDBID($routeid);
      header("Location: index.php?page=routes");
      exit();
   }
   
   
   $routedomainname  = hmailGetPostVar("routedomainname","");
   $routetargetsmtphost   = hmailGetPostVar("routetargetsmtphost","0");
   $routetargetsmtpport   = hmailGetPostVar("routetargetsmtpport","0");
   $TreatSenderAsLocalDomain   = hmailGetPostVar("TreatSenderAsLocalDomain","0");
   $TreatRecipientAsLocalDomain   = hmailGetPostVar("TreatRecipientAsLocalDomain","0");
   
   $routenumberoftries        = hmailGetPostVar("routenumberoftries","0");
   $routemminutesbetweentry   = hmailGetPostVar("routemminutesbetweentry","0");
   $routerequiresauth   = hmailGetPostVar("routerequiresauth","0");
   $routeauthusername   = hmailGetPostVar("routeauthusername","0");
   $routeauthpassword   = hmailGetPostVar("routeauthpassword","0");
   $ConnectionSecurity   = hmailGetPostVar("ConnectionSecurity","0");
   
   $obRoute->DomainName = $routedomainname;
   $obRoute->TargetSMTPHost = $routetargetsmtphost;
   $obRoute->TargetSMTPPort = $routetargetsmtpport;
   $obRoute->TreatSenderAsLocalDomain = $TreatSenderAsLocalDomain;
   $obRoute->TreatRecipientAsLocalDomain = $TreatRecipientAsLocalDomain;
   
   $obRoute->NumberOfTries = $routenumberoftries;
   $obRoute->MinutesBetweenTry = $routemminutesbetweentry;
   $obRoute->RelayerRequiresAuth = $routerequiresauth;
   $obRoute->RelayerAuthUsername = $routeauthusername;
   
   $obRoute->AllAddresses = hmailGetPostVar("AllAddresses","0");
   
   $obRoute->ConnectionSecurity = $ConnectionSecurity;
   
   if ($routeauthpassword != "")
      $obRoute->SetRelayerAuthPassword($routeauthpassword);

   $obRoute->Save();
   
   $routeid = $obRoute->ID;
   
   header("Location: index.php?page=route&action=edit&routeid=$routeid");
?>

