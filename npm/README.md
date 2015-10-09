# SPDeployment

SPDeployment is a command line tool to deploy all kind of files to SharePoint / Office 365.

With the help of a `SPDeployment.json` file in your project you can configure which files in which folders should be deployed to which targets.

## Installation

You can install the SPDeployment tool with npm.
```bash
npm install spdeployment -g
```

## Usage

### Create a SPDeployment.json file

You can use the file in the `sample` folder to get started.

It has the following elements:

```
{
  "DefaultEnvironment": "Test",
  "Sites": [
    {
      "Environment": "Test",
      "Name": "AppForTest",
      "Url": "https://your-tenant.sharepoint.com/sites/test",
      "Username": "",
      "Password": "",
      "Files": [
        {
          "Source": "dist\\Style Library",
          "Destination": "/Style Library",
          "Exclude": ".*.bundle,.*.map"
        }
      ]
    }
  ]
```

With `DefaultEnvironment` you can specify which environment should be deployed when you run `spd` without any parameters.
Then you can specify multiple sites which must have the following parameters:

* Environment : Any string to define an environment
* Name : Any string to define a name for this site
* Url : The target site url
* Username : The username or an empty string. If it is empty, `spd` will prompt for it.
* Password : The password or an empty string. If it is empty, `spd` will prompt for it.
* Files : An array containing
    * the local source folder (with escaped \\)
    * the remote destination folder (in url format with /)
    * Regex to exclude files/folders

Now add this file to your project root.

### Run it

To run it, open a command line within your project root folder and run:

* `spd` without any parameters to deploy all sites for the default environment
* `spd env:yourenvname` to deploy all sites with the `yourenvname` environment
* `spd name:somename` to deploy the site with the `somename` name