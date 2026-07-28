<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != ADMIN_SERVER)
   	hmailHackingAttemp(); // Only server can change these settings.      

   hmailRequirePostCsrfToken();

   $domainid	= hmailGetPostVar("domainid",0,true);
   $aliasid	   = hmailGetPostVar("aliasid",0);
   $action	   = hmailGetPostVar("action","");
   $aliasname  = hmailGetPostVar("aliasname","");

   $obDomain	= $obBaseApp->Domains->ItemByDBID($domainid);
    
   if ($action == "add")
   {
      $alias =  $obDomain->DomainAliases->Add();
      $alias->AliasName = $aliasname;
      $alias->Save();
   }
   elseif ($action == "delete")
   {
      $obDomain->DomainAliases->DeleteByDBID($aliasid);
   }
   
   header("Location: index.php?page=domain&action=edit&domainid=$domainid");
?>

