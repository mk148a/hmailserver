<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != 2)
   	hmailHackingAttemp(); // Only server admins can change this.

   hmailRequirePostCsrfToken();
   
   $ID 		= hmailGetPostVar("ID",0);
   $action	      = hmailGetPostVar("action","");
   
   $obWhiteListAddresses	= $obBaseApp->Settings()->AntiSpam()->WhiteListAddresses;

   if ($action == "edit")
      $obAddress = $obWhiteListAddresses->ItemByDBID($ID);  
   elseif ($action == "add")
      $obAddress = $obWhiteListAddresses->Add();  
   elseif ($action == "delete")
   {
      $obWhiteListAddresses->DeleteByDBID($ID);  
      header("Location: index.php?page=whitelistaddresses");
      exit();
   }
      
   $LowerIPAddress = hmailGetPostVar("LowerIPAddress",0);
   $UpperIPAddress = hmailGetPostVar("UpperIPAddress",0);
   $EmailAddress   = hmailGetPostVar("EmailAddress",0);
   $Description    = hmailGetPostVar("Description",0);
   
   if ($LowerIPAddress == "")
      $LowerIPAddress = "0.0.0.0";
   
   if ($UpperIPAddress == "")
      $UpperIPAddress = "255.255.255.255";

   if ($EmailAddress == "")
      $EmailAddress = "*";

   $obAddress->LowerIPAddress  = $LowerIPAddress;
   $obAddress->UpperIPAddress  = $UpperIPAddress;
   $obAddress->EmailAddress    = $EmailAddress;
   $obAddress->Description     = $Description;
   
   $obAddress->Save();
   
   
   
   header("Location: index.php?page=whitelistaddresses");
?>

