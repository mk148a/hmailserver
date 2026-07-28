<?php
   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != ADMIN_SERVER)
   	hmailHackingAttemp(); // The user is not server administrator.

   hmailRequirePostCsrfToken();
   
   $action	   = hmailGetPostVar("action","");
   $id	      = hmailGetPostVar("id",0);
   
   $Name	      = hmailGetPostVar("Name",0);
   $CertificateFile	      = hmailGetPostVar("CertificateFile","");
   $PrivateKeyFile= hmailGetPostVar("PrivateKeyFile","");
   
   $sslCertificates = $obBaseApp->Settings->SSLCertificates;
   
   if ($action == "edit")
      $sslCertificate     = $sslCertificates->ItemByDBID($id);
   elseif ($action == "add")
      $sslCertificate     = $sslCertificates->Add();
   elseif ($action == "delete")
   {
      $sslCertificates->DeleteByDBID($id);
      header("Location: index.php?page=sslcertificates");
   }

   // Save the changes
   $sslCertificate->Name = $Name;
   $sslCertificate->CertificateFile = $CertificateFile;
   $sslCertificate->PrivateKeyFile = $PrivateKeyFile;
   $sslCertificate->Save();
   
   header("Location: index.php?page=sslcertificates");
?>

