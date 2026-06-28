// Copyright (c) 2010 Martin Knafve / hMailServer.com.  
// http://www.hmailserver.com

#include "StdAfx.h"

#include "CustomVirusScanner.h"
#include "../../SMTP/SMTPConfiguration.h"
#include "../Util/ProcessLauncher.h"

#ifdef _DEBUG
#define DEBUG_NEW new(_NORMAL_BLOCK, __FILE__, __LINE__)
#define new DEBUG_NEW
#endif

namespace
{
   HM::String QuoteWindowsCommandLineArgument(const HM::String &argument)
   {
      HM::String quoted = _T("\"");
      int backslashCount = 0;

      for (int i = 0; i < argument.GetLength(); i++)
      {
         TCHAR ch = argument[i];

         if (ch == _T('\\'))
         {
            backslashCount++;
            continue;
         }

         if (ch == _T('"'))
         {
            quoted.append(backslashCount * 2 + 1, _T('\\'));
            quoted += ch;
         }
         else
         {
            quoted.append(backslashCount, _T('\\'));
            quoted += ch;
         }

         backslashCount = 0;
      }

      quoted.append(backslashCount * 2, _T('\\'));
      quoted += _T("\"");

      return quoted;
   }
}

namespace HM
{
   CustomVirusScanner::CustomVirusScanner(void)
   {
   }

   CustomVirusScanner::~CustomVirusScanner(void)
   {

   }
   
   VirusScanningResult 
   CustomVirusScanner::Scan(const String &sFilename)
   {
      AntiVirusConfiguration &pConfig = Configuration::Instance()->GetAntiVirusConfiguration();
      String executablePath = pConfig.GetCustomScannerExecutable();
      int virusReturnCode = pConfig.GetCustomScannerReturnValue();
      return Scan(executablePath, virusReturnCode, sFilename);
   }

   VirusScanningResult 
   CustomVirusScanner::Scan(const String &executablePath, int virusReturnCode, const String &sFilename)
   {
      LOG_DEBUG("Running custom virus scanner...");


      String sPath = FileUtilities::GetFilePath(sFilename);

      String sCommandLine;
      String quotedFilename = QuoteWindowsCommandLineArgument(sFilename);

      if (executablePath.Find(_T("\"%FILE%\"")) >= 0)
      {
         sCommandLine = executablePath;
         sCommandLine.Replace(_T("\"%FILE%\""), quotedFilename);
      }
      else if (executablePath.Find(_T("%FILE%")) >= 0)
      {
         sCommandLine = executablePath;
         sCommandLine.Replace(_T("%FILE%"), quotedFilename);
      }
      else
         sCommandLine.Format(_T("%s %s"), executablePath.c_str(), quotedFilename.c_str());

      unsigned int exitCode = 0;
      ProcessLauncher launcher(sCommandLine, sPath);
      launcher.SetErrorLogTimeout(20000);
      if (!launcher.Launch(exitCode))
      {
         return VirusScanningResult("CustomVirusScanner::Scan", "Unable to launch executable.");
      }

      String sDebugMessage = Formatter::Format("Scanner: {0}. Return code: {1}", sCommandLine, exitCode);
      LOG_DEBUG(sDebugMessage);

      if (exitCode == virusReturnCode)
         return VirusScanningResult(VirusScanningResult::VirusFound, "Unknown");
      else
         return VirusScanningResult(VirusScanningResult::NoVirusFound, Formatter::Format("Return code: {0}", exitCode));

   }
}
