# Changelog

## 1.0.7
- Fix: Dynamically adjust max choices shown in hiring dialogue based on the UI scale to prevent the dialogue from becoming unusable at higher scales
- Feat: Add configuration options for laundering capacities per Business
## 1.0.6
- Fix: Minor bug where refilling safes could throw an error if no valid safes were found
## 1.0.5
- Hotfix for <0.4.4 - compile against lower version assemblies
## 1.0.4
- Hotfix: Compile against S1API 2.9.3 - accidentally pinned the version by compiling against the latest
## 1.0.3
- Lower max Golden Safe price in the validator to 100k
## 1.0.2
- Add more guarding against potential nullrefs when searching for safes and other containers with cash.
- Add a notification when safes are refilled.
## 1.0.1
- Add configuration option for autorefilling the Golden Safe when sleeping.
- Improve Golden Safe search
## 1.0.0
- initial
