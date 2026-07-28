<?php
   
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() == 0)
      hmailHackingAttemp();

   hmailRequirePostCsrfToken();

   $domainid	= hmailGetPostVar("domainid",0,true);
   $aliasid 	= hmailGetPostVar("aliasid",0);
   $action	   = hmailGetPostVar("action","");
   
   if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())
   	hmailHackingAttemp(); // Domain admin but not for this domain.

   $obDomain	= $obBaseApp->Domains->ItemByDBID($domainid);

   if ($action == "add")
   {
      $result = IsAddAllowed($obDomain);
      
      if ($result > 0)
      {
         header("Location: index.php?page=alias&action=$action&domainid=$domainid&aliasid=$aliasid&error_message=$result");  
         exit();        
      } 
      
   }   	
   	
   if ($action == "edit")
      $obAlias = $obDomain->Aliases->ItemByDBID($aliasid);  
   elseif ($action == "add")
      $obAlias = $obDomain->Aliases->Add();  
   elseif ($action == "delete")
   {
      $obDomain->Aliases->DeleteByDBID($aliasid);  
      header("Location: index.php?page=aliases&domainid=$domainid");
      exit();
   }
   
   $domainname = $obDomain->Name;
   	
   $aliasname    = hmailGetPostVar("aliasname","");
   $aliasvalue   = hmailGetPostVar("aliasvalue","");
   $aliasactive  = hmailGetPostVar("aliasactive","0");
   
   $obAlias->Name = $aliasname . "@" . $domainname;
   $obAlias->Value = $aliasvalue;
   $obAlias->Active = $aliasactive;
   
   $obAlias->Save();
   $aliasid = $obAlias->ID;
   
   header("Location: index.php?page=alias&action=edit&domainid=$domainid&aliasid=$aliasid");
   
   function IsAddAllowed($obDomain)
   {
      if (!$obDomain->MaxNumberOfAliasesEnabled)
         return 0;
      
      if ($obDomain->Aliases->Count >= $obDomain->MaxNumberOfAliases)
         return STR_ALIAS_COULD_NOT_BE_ADDED_MAX_REACHED;
        
      return 0;
   }   
?>

