# Archive / Retirement Notice

> Please use the new and improved version of this app:
> [caleb9/syno-photos-util](https://github.com/caleb9/syno-photos-util).


# Synology Photos Util

* List folders containing photos in a Synology Photos album
* Copy album contents into a Synology Photos folder


## Why?

This is a simple console app querying Synology Photos API to deduce
locations (folder paths) of photos added to a Synology Photos *album*
or copy photos added to an album to a folder.

I used it while doing some spring cleaning of photos on my Synology
NAS. Maybe someone will find it handy as well.


## How?

Download the archive for your platform from Releases, unzip and
execute

```./SynologyPhotosUtil --help```

or on Windows

```SynologyPhotosUtil.exe --help```

This will display a help message about required command line
arguments:

```
Global options:
    -h, --help                          Prints this message
    -v, --version                       Print version information

Commands:
    list <ALBUM-NAME>                   List photos in album
    export <ALBUM-NAME> <FOLDER-PATH>   Copy photos to a folder in personal space
    
Common command options (available for all commands):
    -a, --address <URL>                 [REQUIRED] HTTP(S) address of Synology DSM
    -u, --user <USER-NAME>              [REQUIRED] DSM user account name
    -p, --password <PASSWORD>           [REQUIRED] DSM user account password
    -o, --otp <OTP-CODE>                OTP code when 2FA is enabled for user account

<ALBUM-NAME> and <FOLDER-PATH> are case sensitive. <FOLDER-PATH> must exist in user's personal space. 
<ALBUM-NAME> can be a person in "People" album.
```

* ALBUM-NAME as it stands in Synology Photos, enclose in quotes if the
  name contains any spaces

See the usage example below.


## Usage example

### Listing an album

Assuming a DSM user account is named "my_user" and has access to
Synology Photos album "My Album", the application can be executed in
the following way:

```
./SynologyPhotosUtil list "My Album" -a http://diskstation.address -u my_user -p my_password
```

**Note**: The address value should be the same as the one you use to
open DSM in your browser. Unless you use non-standard ports (5000 for
HTTP and 5001 for HTTPS), you can omit the port, otherwise it needs to
be specified e.g. http://diskstation.address:5042.

Alternatively, if your account has two factor authentication enabled,
you must also provide a one time code from your authenticator app with
the `-o` argument e.g.:

```
./SynologyPhotosUtil list "My Album" -a http://diskstation.address -u my_user -p my_password -o 123456
```

Depending on the connection speed, and how many photos are added to
"My Album", querying the API can take a moment. The output can look
like this, for example:

```
P: /personal_space_folder/my_photos/IMG_1111.jpg
S: /shared_space_folder/IMG_2222.JPG
ERROR: IMG_3333.jpeg folder inaccessible
```

In this example "My Album" contains 3 photos:
* The the IMG\_1111.jpg photo is located in one of the user's
  "personal space" folders (indicated by leading `P:`), i.e. (by
  default) under `{your user's home directory}/Photos` in a
  `personal_space_folder/my_photos` folder.
* The IMG\_2222.JPG photo is located in "shared space" (indicated by
  leading `S:`), i.e. under `photo` shared folder in
  `shared_space_folder`.
* In case of IMG\_3333.jpeg the physical location of the file is
  inaccessible to my\_user. This happens e.g. when there are other NAS
  users having *provider* access to "My Album" and they added photos
  from their personal space. Other possibility is that "My Album" has
  been created by another user and shared with my\_user - depending on
  permissions, some or all of the photo locations may be inaccessible
  for my\_user. These photos are listed at the end of the output with
  a leading `ERROR` message.


### Exporting an album

```
./SynologyPhotosUtil export "My Album" "/folder A/folder B" -a http://diskstation.address -u my_user -p my_password
```

**Note that currently `/folder A/folder B` needs to already exist in
user's personal space.**

The command schedules a *background task* to copy the photos, so the
output needs to be inspected in Synology Photos web interface
(Background Tasks icon in the top right region) - remember to refresh
the page to see it. Photos inaccessible due to permissions will not be
copied. If there are identically named photos in the target folder
already, they will not get overwritten.


## TODO

* Add add-photo-to-album command
* Add optional debug information
* Improve output of export command
* Add support for "Places" albums


## Code disclaimer

This is my first attempt at programming in F#. I'm coming from C#
world and I used this project as a learning exercise. The code could
probably be written in more functional way but I'm still learning. I'm
very open for feedback in this regard.


## Credits

* [zeichensatz/SynologyPhotosAPI](https://github.com/zeichensatz/SynologyPhotosAPI)
  contains description of the Synology Photos API that got me started
* [fsharpforfunandprofit.com](https://fsharpforfunandprofit.com) is a
  fantastic resource where I learned the F# basics
