ALTER TABLE `hm_fetchaccounts` ADD COLUMN `famimerecipientheaders` nvarchar(255) NOT NULL DEFAULT 'To,CC,X-RCPT-TO,X-Envelope-To'

update hm_dbversion set value = 5704