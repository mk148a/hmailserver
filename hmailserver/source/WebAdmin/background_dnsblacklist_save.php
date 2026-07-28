<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != ADMIN_SERVER)
   	hmailHackingAttemp(); // The user is not server administrator.

   hmailRequirePostCsrfToken();
   
	$action	   = hmailGetPostVar("action","");
   $id	      = hmailGetPostVar("id",0);
   $Active	      = hmailGetPostVar("Active",0);
   $DNSHost	      = hmailGetPostVar("DNSHost","");
   $ExpectedResult= hmailGetPostVar("ExpectedResult","");
	$RejectMessage	= hmailGetPostVar("RejectMessage","");
   $Score	      = hmailGetPostVar("Score",0);
   
   $dnsBlackLists = $obBaseApp->Settings->AntiSpam->DNSBlackLists;
   
   if ($action == "edit")
      $dnsBlackList     = $dnsBlackLists->ItemByDBID($id);
   elseif ($action == "add")
      $dnsBlackList     = $dnsBlackLists->Add();
   elseif ($action == "delete")
   {
      $dnsBlackLists->DeleteByDBID($id);
      header("Location: index.php?page=dnsblacklists");
   }

   // Save the changes
   $dnsBlackList->Active = $Active;
   $dnsBlackList->DNSHost = $DNSHost;
   $dnsBlackList->ExpectedResult = $ExpectedResult;
   $dnsBlackList->RejectMessage = $RejectMessage;
   $dnsBlackList->Score = $Score;   

   $dnsBlackList->Save();
   
   $id = $dnsBlackList->ID;
   
   header("Location: index.php?page=dnsblacklists");
?>

