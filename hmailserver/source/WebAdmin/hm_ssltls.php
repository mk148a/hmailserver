<?php
if (!defined('IN_WEBADMIN'))
   exit();

if (hmailGetAdminLevel() != 2)
	hmailHackingAttemp();

$obSettings	= $obBaseApp->Settings();

$action	   = hmailGetPostVar("action","");

if($action == "save")
{
	hmailRequirePostCsrfToken();
	$obSettings->VerifyRemoteSslCertificate= hmailGetPostVar("VerifyRemoteSslCertificate",0);
	$obSettings->SslCipherList = hmailGetPostVar("SslCipherList", "");
	
	$obSettings->TlsVersion10Enabled = hmailGetPostVar("TlsVersion10Enabled", 0);
	$obSettings->TlsVersion11Enabled = hmailGetPostVar("TlsVersion11Enabled", 0);
	$obSettings->TlsVersion12Enabled = hmailGetPostVar("TlsVersion12Enabled", 0);
	$obSettings->TlsVersion13Enabled = hmailGetPostVar("TlsVersion13Enabled", 0);

	$obSettings->TlsOptionPreferServerCiphersEnabled = hmailGetPostVar("TlsOptionPreferServerCiphersEnabled", 0);
	if ((hmailGetPostVar("TlsVersion12Enabled", 0) > 0 || hmailGetPostVar("TlsVersion13Enabled", 0) > 0) && hmailGetPostVar("TlsOptionPreferServerCiphersEnabled", 0) > 0) {
		$obSettings->TlsOptionPrioritizeChaChaEnabled = hmailGetPostVar("TlsOptionPrioritizeChaChaEnabled", 0);
	}
	else {
		$obSettings->TlsOptionPrioritizeChaChaEnabled = 0;
	}
}

$VerifyRemoteSslCertificate = $obSettings->VerifyRemoteSslCertificate;      
$SslCipherList 				= $obSettings->SslCipherList;
$TlsVersion10Enabled 		= $obSettings->TlsVersion10Enabled;
$TlsVersion11Enabled 		= $obSettings->TlsVersion11Enabled;
$TlsVersion12Enabled 		= $obSettings->TlsVersion12Enabled;
$TlsVersion13Enabled 		= $obSettings->TlsVersion13Enabled;
$TlsOptionPreferServerCiphersEnabled		= $obSettings->TlsOptionPreferServerCiphersEnabled;
$TlsOptionPrioritizeChaChaEnabled		= $obSettings->TlsOptionPrioritizeChaChaEnabled && ($obSettings->TlsVersion12Enabled || $obSettings->TlsVersion13Enabled) && $obSettings->TlsOptionPreferServerCiphersEnabled;
?>

<h1><?php EchoTranslation("Security")?></h1>

<form action="index.php" method="post" onSubmit="return formCheck(this);">
   <?php
      PrintHiddenCsrfToken();
      PrintHidden("page", "ssltls");
      PrintHidden("action", "save");
   ?>   
   
   <div class="tabber">
      <div class="tabbertab">
         <h2><?php EchoTranslation("General")?></h2>            
   
      	<table border="0" width="100%" cellpadding="5">
            <tr>
               <th width="30%"></th>
               <th width="70%"></th>
            </tr>            

			<?php
				PrintPropertyAreaRow("SslCipherList", "SSL/TLS ciphers", $SslCipherList, 12, 80);
				PrintCheckboxRow("VerifyRemoteSslCertificate", "Verify remote server SSL/TLS certificates", $VerifyRemoteSslCertificate);
				PrintCheckboxRow("TlsVersion10Enabled", "TLS v1.0", $TlsVersion10Enabled);
				PrintCheckboxRow("TlsVersion11Enabled", "TLS v1.1", $TlsVersion11Enabled);
				PrintCheckboxRow("TlsVersion12Enabled", "TLS v1.2", $TlsVersion12Enabled);
				PrintCheckboxRow("TlsVersion13Enabled", "TLS v1.3", $TlsVersion13Enabled);
				PrintCheckboxRow("TlsOptionPreferServerCiphersEnabled", "Prefer server cipher order", $TlsOptionPreferServerCiphersEnabled);
				PrintCheckboxRow("TlsOptionPrioritizeChaChaEnabled", "Prioritize ChaCha20-Poly1305 when client prefers it (requires TLS v1.2 or TLS v1.3)", $TlsOptionPrioritizeChaChaEnabled);
			?>

      	</table>
      </div>
   </div>   
   <?php
      PrintSaveButton();
   ?>
     
</form>
