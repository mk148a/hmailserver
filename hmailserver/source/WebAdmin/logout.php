<?php
define('IN_WEBADMIN', true);
define('CSRF_ENABLED', true);
require_once("config.php");
require_once("include/functions.php");

session_start();
ensure_csrf_session_token_exists();
hmailRequirePostCsrfToken();
session_destroy();

?>

<html>
	<head>
		<title></title>
		<meta http-equiv='refresh' content='0;URL=<?php echo $hmail_config['rooturl'] . "index.php"?>'>
	</head>
	<body>
	
	</body>
</html>
