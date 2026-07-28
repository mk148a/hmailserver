<?php

   if (!defined('IN_WEBADMIN'))
      exit();

   hmailRequirePostCsrfToken();

   $domainid	= hmailGetPostVar("domainid",0,true);
   $accountid	= hmailGetPostVar("accountid",0,true);
   $action	   = hmailGetPostVar("action","");
   
   $obDomain	= $obBaseApp->Domains->ItemByDBID($domainid);
   
   if (hmailGetAdminLevel() == 0 && ($accountid != hmailGetAccountID() || $action != "edit"))
      hmailHackingAttemp();
   
   if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())
   	hmailHackingAttemp(); // Domain admin but not for this domain.
   	
   $accountpassword  = hmailGetPostVar("accountpassword","");
   $accountmaxsize   = hmailGetPostVar("accountmaxsize","0");
   $accountaddress   = hmailGetPostVar("accountaddress","") . "@". $obDomain->Name;
   $accountactive    = hmailGetPostVar("accountactive","0");
   $accountadminlevel  = hmailGetPostVar("accountadminlevel","0");
   $PersonFirstName  = hmailGetPostVar("PersonFirstName","0");
   $PersonLastName   = hmailGetPostVar("PersonLastName","0");
   
   $vacationmessageon  = hmailGetPostVar("vacationmessageon","");
   $vacationsubject   = hmailGetPostVar("vacationsubject","0");
   $vacationmessage   =   hmailGetPostVar("vacationmessage","");
   $vacationmessageexpires   =   hmailGetPostVar("vacationmessageexpires","0");
   $vacationmessageexpiresdate   =   hmailGetPostVar("vacationmessageexpiresdate","2001-01-01");
   $vacationmessageabortspamflagged = hmailGetPostVar("vacationmessageabortspamflagged","0");
   
   $forwardenabled  = hmailGetPostVar("forwardenabled","0");
   $forwardaddress   = hmailGetPostVar("forwardaddress","");
   $forwardkeeporiginal   =   hmailGetPostVar("forwardkeeporiginal","0");
   $forwardabortspamflagged = hmailGetPostVar("forwardabortspamflagged","0");
   
   $adenabled   = hmailGetPostVar("adenabled","");
   $addomain    = hmailGetPostVar("addomain","0");
   $adusername  =   hmailGetPostVar("adusername","");
  
   $SignatureEnabled     = hmailGetPostVar("SignatureEnabled","0");
   $SignatureHTML        = hmailGetPostVar("SignatureHTML","");
   $SignaturePlainText   =   hmailGetPostVar("SignaturePlainText","0");

  
   if ($action == "edit")
      $obAccount = $obDomain->Accounts->ItemByDBID($accountid);  
   elseif ($action == "add")
      $obAccount = $obDomain->Accounts->Add();  
   elseif ($action == "delete")
   {
      $obAccount = $obDomain->Accounts->DeleteByDBID($accountid);  
      header("Location: index.php?page=accounts&domainid=$domainid");
      exit();
   }
  
   // If this is the current user, we need to update the session password.
   if ($action == "edit" &&
       $accountid == hmailGetAccountID())
   {
      if ($accountpassword != "")
         $_SESSION['session_password'] = $accountpassword;  
   }
   
   if ($accountpassword != "")
      $obAccount->Password = "$accountpassword";
   
   $obAccount->PersonFirstName = $PersonFirstName;
   $obAccount->PersonLastName = $PersonLastName;
   
   $obAccount->VacationMessageIsOn = $vacationmessageon == "1";
   $obAccount->VacationSubject     = $vacationsubject;
   $obAccount->VacationMessage     = $vacationmessage;
   $obAccount->VacationMessageExpires      = $vacationmessageexpires;
   $obAccount->VacationMessageExpiresDate  = $vacationmessageexpiresdate;
   $obAccount->VacationMessageAbortSpamFlagged = $vacationmessageabortspamflagged == "1";

   $obAccount->ForwardEnabled		= $forwardenabled == "1";
   $obAccount->ForwardAddress		= $forwardaddress;
   $obAccount->ForwardKeepOriginal	= $forwardkeeporiginal == "1";
   $obAccount->ForwardAbortSpamFlagged = $forwardabortspamflagged == "1";

   $obAccount->SignatureEnabled		= $SignatureEnabled == "1";
   $obAccount->SignatureHTML		   = $SignatureHTML;
   $obAccount->SignaturePlainText	= $SignaturePlainText;
     
   
   if (hmailGetAdminLevel() != ADMIN_USER)
   {
      $accountmaxsize = str_replace(".", ",", $accountmaxsize);

      // Save other properties
      $obAccount->Address = $accountaddress;
      $obAccount->MaxSize = $accountmaxsize;
      $obAccount->Active  = $accountactive;
      
      $obAccount->IsAD         = $adenabled == "1";
      $obAccount->ADDomain     = $addomain;
      $obAccount->ADUsername   = $adusername;   
      
      if (hmailGetAdminLevel() == 1)
      {
         // The web user is domain administrator. Don't allow him
         // to change the user to server admin, unless he already
         // is this.
         
         if ($accountadminlevel == 0 || $accountadminlevel == 1)
         {
            $obAccount->AdminLevel = $accountadminlevel;
         }
      }
      else if (hmailGetAdminLevel() == 2)
      {
         // The web user is server administrator. Allow any change
         $obAccount->AdminLevel = $accountadminlevel;
      }
   }
   
   
   $obAccount->Save();
   $accountid = $obAccount->ID;
   
   header("Location: index.php?page=account&action=edit&domainid=$domainid&accountid=$accountid");
   

?>

