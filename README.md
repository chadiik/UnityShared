# UnityShared
A collection of folders containing scripts I use in several projects


Setup steps reminder:

Clone repo to ProjectRoot/Extras/UnityShared

git checkout to some branch, ie: devUtils for DevUtils assets

git pull origin devUtils

Create folder in Assets, ie: 'Assets/chadiik

From within folder,

----- If on bash -> start a cmd process with: cmd \c

Then create a symlink with:

mklink /D DevUtils ..\..\Extras\UnityShared\Assets\chadiik\DevUtils\

