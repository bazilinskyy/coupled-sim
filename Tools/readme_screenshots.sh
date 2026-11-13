#!/bin/bash
# Takes the README's editor and simulator screenshots on macOS (screen must be unlocked).
# Starts the Unity editor with ReadmeScreenshots.Run, takes a picture of each window it asks for
# (ReadmeShots/request.txt: "name x y width height") into ReadmeShots/raw/, and answers in ack.txt.
# Usage: Tools/readme_screenshots.sh [path to Unity]
cd "$(dirname "$0")/.." || exit 1
UNITY=${1:-/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity}
rm -rf ReadmeShots; mkdir -p ReadmeShots/raw
"$UNITY" -projectPath "$PWD" -executeMethod ReadmeScreenshots.Run -logFile ReadmeShots/unity.log &
PID=$!
last=""
while kill -0 $PID 2>/dev/null; do
  req=$(cat ReadmeShots/request.txt 2>/dev/null)
  name=${req%% *}
  if [ "$req" = "DONE" ]; then break; fi
  if [ -n "$name" ] && [ "$name" != "$last" ]; then
    read -r n x y w h <<< "$req"
    # Unity in front, so no other window covers it and the Game view renders
    osascript -e 'tell application "Unity" to activate' >/dev/null 2>&1; sleep 1.5
    screencapture -x -o -R "$x,$y,$w,$h" "ReadmeShots/raw/$n.png" && echo "took $n ($w x $h)"
    echo "$n" > ReadmeShots/ack.txt
    last=$name
  fi
  sleep 0.5
done
wait $PID 2>/dev/null
echo "pictures in ReadmeShots/raw"
