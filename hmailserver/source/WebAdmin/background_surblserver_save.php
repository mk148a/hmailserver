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
   $RejectMessage	= hmailGetPostVar("RejectMessage","");
   $Score	      = hmailGetPostVar("Score",0);
   
   $surblServers = $obBaseApp->Settings->AntiSpam->SURBLServers;
   
   if ($action == "edit")
      $surblServer     = $surblServers->ItemByDBID($id);
   elseif ($action == "add")
      $surblServer     = $surblServers->Add();
   elseif ($action == "delete")
   {
      $surblServers->DeleteByDBID($id);
      header("Location: index.php?page=surblservers");
   }

   // Save the changes
   $surblServer->Active = $Active;
   $surblServer->DNSHost = $DNSHost;
   $surblServer->RejectMessage = $RejectMessage;
   $surblServer->Score = $Score;   

   $surblServer->Save();
   
   header("Location: index.php?page=surblservers");
?>

