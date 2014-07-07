﻿// Copyright (c) 2010 Martin Knafve / hMailServer.com.  
// http://www.hmailserver.com

using System;
using System.IO;
using NUnit.Framework;
using RegressionTests.Shared;
using hMailServer;

namespace RegressionTests.IMAP
{
   [TestFixture]
   public class MessageUids : TestFixtureBase
   {
      private void CreateMessageModificationRule(hMailServer.Rules ruleContainer)
      {
         Rule oRule = ruleContainer.Add();
         oRule.Name = "Criteria test";
         oRule.Active = true;

         RuleCriteria oRuleCriteria = oRule.Criterias.Add();
         oRuleCriteria.UsePredefined = true;
         oRuleCriteria.PredefinedField = eRulePredefinedField.eFTMessageSize;
         oRuleCriteria.MatchType = eRuleMatchType.eMTGreaterThan;
         oRuleCriteria.MatchValue = "0";
         oRuleCriteria.Save();

         RuleAction oRuleAction = oRule.Actions.Add();
         oRuleAction.Type = eRuleActionType.eRARunScriptFunction;
         oRuleAction.ScriptFunction = "ModifyMessage";
         oRuleAction.Save();

         oRule.Save();

         File.WriteAllText(_settings.Scripting.CurrentScriptFile,
                           "Sub ModifyMessage(oMessage)" + Environment.NewLine +
                           "oMessage.Subject = \"[Spam] \" + CStr(oMessage.Subject)" + Environment.NewLine +
                           "oMessage.Save" + Environment.NewLine +
                           "End Sub");

         _settings.Scripting.Reload();
      }

      private void CreateMoveRule(hMailServer.Rules ruleContainer, string foldername)
      {
         Rule oRule = ruleContainer.Add();
         oRule.Name = "Criteria test";
         oRule.Active = true;

         RuleCriteria oRuleCriteria = oRule.Criterias.Add();
         oRuleCriteria.UsePredefined = true;
         oRuleCriteria.PredefinedField = eRulePredefinedField.eFTMessageSize;
         oRuleCriteria.MatchType = eRuleMatchType.eMTGreaterThan;
         oRuleCriteria.MatchValue = "0";
         oRuleCriteria.Save();

         RuleAction oRuleAction = oRule.Actions.Add();
         oRuleAction.Type = eRuleActionType.eRAMoveToImapFolder;
         oRuleAction.IMAPFolder = foldername;
         oRuleAction.Save();

         oRule.Save();
      }

      [Test]
      [Description("Confirm that new messages receive new UIDs")]
      public void TestBasicIncrements()
      {
         Account testAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "Test'Account@test.com",
                                                                                "test");

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);

         IMAPFolder inboxFolder = testAccount.IMAPFolders[0];

         CustomAssert.AreEqual(1, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(1, inboxFolder.Messages[0].UID);

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 2);

         CustomAssert.AreEqual(2, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(2, inboxFolder.Messages[1].UID);

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 3);

         CustomAssert.AreEqual(3, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(3, inboxFolder.Messages[2].UID);
      }

      [Test]
      [Description("Confirm that deletion of messages does not effect UID sequence")]
      public void TestBasicIncrementsWithDeletion()
      {
         Account testAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "Test'Account@test.com",
                                                                                "test");

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);

         IMAPFolder inboxFolder = testAccount.IMAPFolders[0];

         CustomAssert.AreEqual(1, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(1, inboxFolder.Messages[0].UID);
         POP3Simulator.AssertGetFirstMessageText(testAccount.Address, "test");

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);

         CustomAssert.AreEqual(2, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(2, inboxFolder.Messages[0].UID);
         POP3Simulator.AssertGetFirstMessageText(testAccount.Address, "test");


         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);

         CustomAssert.AreEqual(3, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(3, inboxFolder.Messages[0].UID);
      }

      [Test]
      [Description("Confirm that moving a message to a new folder generates an UID specific to that folder.")]
      public void TestMoveMessageWithAccountLevelRule()
      {
         Account testAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "Test'Account@test.com",
                                                                                "test");

         // First deliver two messages to the inbox.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);
         IMAPFolder inboxFolder = testAccount.IMAPFolders[0];
         CustomAssert.AreEqual(1, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(1, inboxFolder.Messages[0].UID);

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 2);
         CustomAssert.AreEqual(2, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(2, inboxFolder.Messages[1].UID);

         CreateMoveRule(testAccount.Rules, "TestFolder");

         // This message will be moved into the test folder.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");

         // Wait for the message to arrive.
         TestSetup.AssertFolderExists(testAccount.IMAPFolders, "TestFolder");

         IMAPFolder testFolder = testAccount.IMAPFolders.get_ItemByName("TestFolder");

         // Since the message is placed in a new folder, it should receive a unique UID.
         TestSetup.AssertFolderMessageCount(testFolder, 1);
         CustomAssert.AreEqual(1, testFolder.CurrentUID);
         CustomAssert.AreEqual(1, testFolder.Messages[0].UID);
      }

      [Test]
      [Description("Confirm that moving a message to a new folder generates an UID specific to that folder.")]
      public void TestMoveMessageWithGlobalRule()
      {
         Account testAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "Test'Account@test.com",
                                                                                "test");

         // First deliver two messages to the inbox.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);
         IMAPFolder inboxFolder = testAccount.IMAPFolders[0];
         CustomAssert.AreEqual(1, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(1, inboxFolder.Messages[0].UID);

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 2);
         CustomAssert.AreEqual(2, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(2, inboxFolder.Messages[1].UID);

         CreateMoveRule(_application.Rules, "TestFolder");

         // This message will be moved into the test folder.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");

         // Wait for the message to arrive.
         TestSetup.AssertFolderExists(testAccount.IMAPFolders, "TestFolder");

         IMAPFolder testFolder = testAccount.IMAPFolders.get_ItemByName("TestFolder");
         TestSetup.AssertFolderMessageCount(testFolder, 1);

         // Since the message is placed in a new folder, it should receive a unique UID.
         CustomAssert.AreEqual(1, testFolder.Messages.Count);
         CustomAssert.AreEqual(1, testFolder.CurrentUID);
         CustomAssert.AreEqual(1, testFolder.Messages[0].UID);
      }

      [Test]
      [Description("Issue 267, Invalid message UID generated. " +
                   " Confirm that moving a message to a new folder generates an UID specific to that folder, even if the message is saved using an account rule."
         )]
      public void TestSaveMessageWithScriptAndMoveMessageWithAccountRule()
      {
         _settings.Scripting.Enabled = true;
         _settings.Scripting.Reload();

         Account testAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "Test'Account@test.com",
                                                                                "test");

         var sim = new IMAPSimulator();
         CustomAssert.IsTrue(sim.ConnectAndLogon(testAccount.Address, "test"));

         // First deliver two messages to the inbox.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);
         IMAPFolder inboxFolder = testAccount.IMAPFolders[0];
         CustomAssert.AreEqual(1, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(1, inboxFolder.Messages[0].UID);
         CustomAssert.IsTrue(sim.Status("INBOX", "UIDNEXT").Contains("UIDNEXT 2"));

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 2);
         CustomAssert.IsTrue(sim.Status("INBOX", "UIDNEXT").Contains("UIDNEXT 3"));
         CustomAssert.AreEqual(2, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(2, inboxFolder.Messages[1].UID);

         CreateMessageModificationRule(testAccount.Rules);
         CreateMoveRule(testAccount.Rules, "TestFolder");

         // This message will be moved into the test folder.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");

         // Wait for the message to arrive.
         TestSetup.AssertFolderExists(testAccount.IMAPFolders, "TestFolder");

         IMAPFolder testFolder = testAccount.IMAPFolders.get_ItemByName("TestFolder");
         TestSetup.AssertFolderMessageCount(testFolder, 1);

         // The UID for the inbox should be the same as before.
         CustomAssert.IsTrue(sim.Status("INBOX", "UIDNEXT").Contains("UIDNEXT 3"));

         // Since the message is placed in a new folder, it should receive a unique UID.
         CustomAssert.IsTrue(sim.Status("TestFolder", "UIDNEXT").Contains("UIDNEXT 2"));
         CustomAssert.AreEqual(1, testFolder.Messages.Count);
         CustomAssert.AreEqual(1, testFolder.CurrentUID);
         CustomAssert.AreEqual(1, testFolder.Messages[0].UID);
      }

      [Test]
      [Description("Issue 267, Invalid message UID generated. " +
                   " Confirm that moving a message to a new folder generates an UID specific to that folder, even if the message is saved using an account rule."
         )]
      public void TestSaveMessageWithScriptAndMoveMessageWithGlobalRule()
      {
         _settings.Scripting.Enabled = true;

         Account testAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "Test'Account@test.com",
                                                                                "test");

         var sim = new IMAPSimulator();
         CustomAssert.IsTrue(sim.ConnectAndLogon(testAccount.Address, "test"));

         // First deliver two messages to the inbox.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 1);
         IMAPFolder inboxFolder = testAccount.IMAPFolders[0];
         CustomAssert.AreEqual(1, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(1, inboxFolder.Messages[0].UID);
         CustomAssert.IsTrue(sim.Status("INBOX", "UIDNEXT").Contains("UIDNEXT 2"));

         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");
         POP3Simulator.AssertMessageCount(testAccount.Address, "test", 2);
         CustomAssert.AreEqual(2, inboxFolder.CurrentUID);
         CustomAssert.AreEqual(2, inboxFolder.Messages[1].UID);
         CustomAssert.IsTrue(sim.Status("INBOX", "UIDNEXT").Contains("UIDNEXT 3"));

         CreateMessageModificationRule(_application.Rules);
         CreateMoveRule(_application.Rules, "TestFolder");


         // This message will be moved into the test folder.
         SMTPClientSimulator.StaticSend(testAccount.Address, testAccount.Address, "Test", "Test");

         // Wait for the message to arrive.
         TestSetup.AssertFolderExists(testAccount.IMAPFolders, "TestFolder");
         IMAPFolder testFolder = testAccount.IMAPFolders.get_ItemByName("TestFolder");
         TestSetup.AssertFolderMessageCount(testFolder, 1);

         // Inbox UID should not have changed since nothing has been added to the inbox.
         CustomAssert.IsTrue(sim.Status("INBOX", "UIDNEXT").Contains("UIDNEXT 3"));

         // Since the message is placed in a new folder, it should receive a unique UID.
         string status = sim.Status("TestFolder", "UIDNEXT");
         CustomAssert.IsTrue(status.Contains("UIDNEXT 2"), status);
         CustomAssert.AreEqual(1, testFolder.CurrentUID);
         CustomAssert.AreEqual(1, testFolder.Messages[0].UID);
      }
   }
}