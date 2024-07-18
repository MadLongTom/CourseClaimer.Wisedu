a ubuntu docker img with .netsdk8 and opencv

for running this interestring software in linux docker

using `docker build -t heujwxk .` in cli to build docker img

and then use `docker container run --name test -itd -p 5074:5074  heujwxk:latest /bin/bash /run.sh` to start the project

however, there is no entry for persistence the Databace