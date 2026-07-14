<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   hmailRequirePostCsrfToken();

   $domainid	= hmailGetPostVar("domainid",0, true);
   $accountid 	= hmailGetPostVar("accountid",0,true);
   $faid 		= hmailGetPostVar("faid",0, true);
   $action	   = hmailGetPostVar("action","");
   
   if (hmailGetAdminLevel() == 0 && ($accountid != hmailGetAccountID() || $domainid != hmailGetDomainID()))
      hmailHackingAttemp();

	if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())
		hmailHackingAttemp(); // Domain admin but not for this domain.
	
	$obDomain	= $obBaseApp->Domains->ItemByDBID($domainid);
	$obAccount  = $obDomain->Accounts->ItemByDBID($accountid);  
	$obFetchAccounts = $obAccount->FetchAccounts();

   if ($action == "edit")
   {
      $obFA = $obFetchAccounts->ItemByDBID($faid);  
      $OldServerAddress = $obFA->ServerAddress;
      $OldPort = $obFA->Port;
      $OldUsername = $obFA->Username;
      $OldConnectionSecurity = $obFA->ConnectionSecurity;
   }
   elseif ($action == "add")
      $obFA = $obFetchAccounts->Add();  
   elseif ($action == "delete")
   {
      hmailRequirePost();
      $obFetchAccounts->DeleteByDBID($faid);  
      header("Location: index.php?page=account_externalaccounts&domainid=$domainid&accountid=$accountid");
      exit();
   }
   elseif ($action == "downloadnow")
   {
      hmailRequirePost();
      $obFA = $obFetchAccounts->ItemByDBID($faid); 
      $obFA->DownloadNow();
      header("Location: index.php?page=account_externalaccounts&domainid=$domainid&accountid=$accountid");
      exit();       
   }
   
   $DaysToKeepMessages      = hmailGetPostVar("DaysToKeepMessages",0);
   $DaysToKeepMessagesValue = hmailGetPostVar("DaysToKeepMessagesValue",0);
   
   $obFA->Enabled               = hmailGetPostVar("Enabled",0);
   $obFA->Name                  = hmailGetPostVar("Name",0);
   $obFA->MinutesBetweenFetch   = hmailGetPostVar("MinutesBetweenFetch",0);
   $obFA->Port                  = hmailGetPostVar("Port",0);
   $obFA->MIMERecipientHeaders  = hmailGetPostVar("MIMERecipientHeaders","To,CC,X-RCPT-To,X-Envelope-To");
   if (strlen($obFA->MIMERecipientHeaders) > 0)
      $obFA->ProcessMIMERecipients = hmailGetPostVar("ProcessMIMERecipients",0);
   else
      $obFA->ProcessMIMERecipients = 0;
   $obFA->ProcessMIMEDate       = hmailGetPostVar("ProcessMIMEDate",0);
   $obFA->ServerAddress         = hmailGetPostVar("ServerAddress",0);
   $obFA->ServerType            = hmailGetPostVar("ServerType",0);
   $obFA->Username              = hmailGetPostVar("Username",0);
   $obFA->UseAntiVirus          = hmailGetPostVar("UseAntiVirus",0);
   $obFA->UseAntiSpam           = hmailGetPostVar("UseAntiSpam",0);
   if ($obFA->ProcessMIMERecipients != 0)
      $obFA->EnableRouteRecipients = hmailGetPostVar("EnableRouteRecipients",0);
   else
      $obFA->EnableRouteRecipients = 0;
   $obFA->ConnectionSecurity 	= hmailGetPostVar("ConnectionSecurity",0);
   
   if (strlen($DaysToKeepMessages) > 0 && $DaysToKeepMessages <= 0)
      $obFA->DaysToKeepMessages = $DaysToKeepMessages; 
   else 
      $obFA->DaysToKeepMessages = $DaysToKeepMessagesValue; 
   
   $Password = hmailGetPostVar("Password","");
   
   if (strlen($Password) > 0)
      $obFA->Password = $Password;
   elseif ($action == "edit" &&
           (strcmp((string) $OldServerAddress, (string) $obFA->ServerAddress) !== 0 ||
            (int) $OldPort !== (int) $obFA->Port ||
            strcmp((string) $OldUsername, (string) $obFA->Username) !== 0 ||
            (int) $OldConnectionSecurity !== (int) $obFA->ConnectionSecurity))
      $obFA->Password = "";
   
   $obFA->Save();
   
   $faid = $obFA->ID;
   
   
   
   header("Location: index.php?page=account_externalaccount&action=edit&domainid=$domainid&accountid=$accountid&faid=$faid");
?>

