<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   hmailRequirePostCsrfToken();

   $domainid	= hmailGetPostVar("domainid",0,true);
   $action	   = hmailGetPostVar("action","");
   $domainname     = hmailGetPostVar("domainname","");
   $domainactive   = hmailGetPostVar("domainactive","0");
   $domainpostmaster   =   hmailGetPostVar("domainpostmaster","");
   $domainmaxsize   = hmailGetPostVar("domainmaxsize","0");
   $domainmaxmessagesize   = hmailGetPostVar("domainmaxmessagesize","0");
   $domainplusaddressingenabled = hmailGetPostVar("domainplusaddressingenabled","0");
   $domainplusaddressingcharacter = hmailGetPostVar("domainplusaddressingcharacter","+");
   $domainantispamenablegreylisting = hmailGetPostVar("domainantispamenablegreylisting","0");
   
   $SignatureEnabled   = hmailGetPostVar("SignatureEnabled","0");
   $SignatureHTML  	  = hmailGetPostVar("SignatureHTML","");
   $SignaturePlainText = hmailGetPostVar("SignaturePlainText","");
   $SignatureMethod    = hmailGetPostVar("SignatureMethod","1");
   
   $AddSignaturesToLocalMail = hmailGetPostVar("AddSignaturesToLocalMail","0");
   $AddSignaturesToReplies   = hmailGetPostVar("AddSignaturesToReplies","0");
   
   $MaxAccountSize       = hmailGetPostVar("MaxAccountSize","0");
   
   $MaxNumberOfAccounts            = hmailGetPostVar("MaxNumberOfAccounts","0");
   $MaxNumberOfAliases             = hmailGetPostVar("MaxNumberOfAliases","0");
   $MaxNumberOfDistributionLists   = hmailGetPostVar("MaxNumberOfDistributionLists","0");
   
   $MaxNumberOfAccountsEnabled          = hmailGetPostVar("MaxNumberOfAccountsEnabled","0");
   $MaxNumberOfAliasesEnabled           = hmailGetPostVar("MaxNumberOfAliasesEnabled","0");
   $MaxNumberOfDistributionListsEnabled = hmailGetPostVar("MaxNumberOfDistributionListsEnabled","0");
   
   $DKIMSignEnabled = hmailGetPostVar("DKIMSignEnabled", "0");
   $DKIMSignAliasesEnabled = hmailGetPostVar("DKIMSignAliasesEnabled", "0");
   $DKIMPrivateKeyFile = hmailGetPostVar("DKIMPrivateKeyFile", "");
   $DKIMSelector = hmailGetPostVar("DKIMSelector", "");
   
   $DKIMHeaderCanonicalizationMethod = hmailGetPostVar("DKIMHeaderCanonicalizationMethod", "2");
   $DKIMBodyCanonicalizationMethod = hmailGetPostVar("DKIMBodyCanonicalizationMethod", "2");
   $DKIMSigningAlgorithm = hmailGetPostVar("DKIMSigningAlgorithm", "2");
   
   if ($domainactive == "")
      $domainactive = 0;
   
   if (hmailGetAdminLevel() == 1 && ($domainid != hmailGetDomainID() || $action != "edit"))
   	hmailHackingAttemp(); // Domain admin but not for this domain.   

   if ($action == "edit")   
      $obDomain	= $obBaseApp->Domains->ItemByDBID($domainid);
   elseif ($action == "add")
      $obDomain	= $obBaseApp->Domains->Add();
   elseif ($action == "delete")
   {
      if (hmailGetAdminLevel() != ADMIN_SERVER)
         hmailHackingAttemp();

      $obDomain	= $obBaseApp->Domains->ItemByDBID($domainid);
      $obDomain->Delete();
      
      header("Location: index.php?page=domains");
      exit();
      
   }
      
   $obDomain->Postmaster = $domainpostmaster;
   
   $obDomain->PlusAddressingEnabled = $domainplusaddressingenabled == "1";
   $obDomain->PlusAddressingCharacter = $domainplusaddressingcharacter;
   $obDomain->AntiSpamEnableGreylisting = $domainantispamenablegreylisting == "1";
   
   $obDomain->SignatureEnabled   = $SignatureEnabled == "1";
   $obDomain->SignaturePlainText = $SignaturePlainText;
   $obDomain->SignatureHTML      = $SignatureHTML;
   $obDomain->SignatureMethod    = $SignatureMethod;
      
   $obDomain->AddSignaturesToLocalMail = $AddSignaturesToLocalMail;
   $obDomain->AddSignaturesToReplies   = $AddSignaturesToReplies;
   
   $obDomain->DKIMSignEnabled = $DKIMSignEnabled;
   if ($obDomain->DomainAliases->Count > 0){
      $obDomain->DKIMSignAliasesEnabled = $DKIMSignAliasesEnabled;
   }
   else {
      $obDomain->DKIMSignAliasesEnabled = 0;
   }
   $obDomain->DKIMPrivateKeyFile = $DKIMPrivateKeyFile;
   $obDomain->DKIMSelector = $DKIMSelector;
   $obDomain->DKIMHeaderCanonicalizationMethod = $DKIMHeaderCanonicalizationMethod;
   $obDomain->DKIMBodyCanonicalizationMethod = $DKIMBodyCanonicalizationMethod;
   $obDomain->DKIMSigningAlgorithm = $DKIMSigningAlgorithm;
   
   if (hmailGetAdminLevel() == 2)
   {
      // Save other properties
      $obDomain->Active = $domainactive;
      $obDomain->Name = $domainname;
      $obDomain->MaxSize = $domainmaxsize;
      $obDomain->MaxMessageSize = $domainmaxmessagesize;
      $obDomain->MaxAccountSize      = $MaxAccountSize;
      
      $obDomain->MaxNumberOfAccounts = $MaxNumberOfAccounts;
      $obDomain->MaxNumberOfAliases  = $MaxNumberOfAliases;
      $obDomain->MaxNumberOfDistributionLists = $MaxNumberOfDistributionLists;

      $obDomain->MaxNumberOfAccountsEnabled = $MaxNumberOfAccountsEnabled;
      $obDomain->MaxNumberOfAliasesEnabled  = $MaxNumberOfAliasesEnabled;
      $obDomain->MaxNumberOfDistributionListsEnabled = $MaxNumberOfDistributionListsEnabled;
   }

   $obDomain->Save();
   $domainid = $obDomain->ID;
   
   header("Location: index.php?page=domain&action=edit&domainid=$domainid");
?>

