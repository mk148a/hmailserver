<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != ADMIN_SERVER)
   	hmailHackingAttemp(); // The user is not server administrator.

   hmailRequirePostCsrfToken();
   
   $messageid	      = hmailGetPostVar("messageid",0);
   $messagename	   = hmailGetPostVar("messagename",0);
   $messagetext	   = hmailGetPostVar("messagetext",0);
   
   $obServerMessage     = $obBaseApp->Settings->ServerMessages->ItemByDBID($messageid);
   
   if ($obServerMessage->Name != $messagename)
      hmailHackingAttemp();
      
   $obServerMessage->Text = $messagetext;
   $obServerMessage->Save();
   
   header("Location: index.php?page=servermessage&messageid=$messageid");
?>

