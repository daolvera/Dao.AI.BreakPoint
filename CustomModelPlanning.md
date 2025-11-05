# BreakPoint.AI AI Analysis for Tennis

Using AI, generate a player profile and an improvement plan to get better at tennis

## Custom AI Functionality for BreakPoint.AI Swing Coaching

Want to develop a custom ai model to anaylze a person's tennis swing and give them pointed feedback to improve on for that swing type

### Input

Video of a person's single tennis swing and picking what type of swing it is

- forehand ground stroke
- backhand ground stroke
- Serve
- backhand Volley
- forehand volley
- Smash volley
  Requirement: video is from the back for best results, show examples of good and bad

### Output

positive notes about technique
Grade: Overall how technically good was the swing (1.0 -> 7.0)
negative points about technique
Coaching Recommendations: The 3 most relevant tips / drills to improve the swing based on the technique analysis

## Implementation

1. get video from blob storage
2. analyze video for "key" frames (have it pull it out the times where it each occurs then just pull frames from beinning, middle and end of the time it says) using custom swing analyzer model

- preparation
- contact point
- follow through

3. Send key frames to custom "Coaching Model"
4. the resulting technique analysis / grade then is sent to a custom llm model that just takes in info and provides coaching recs

## Custom AI Functionality for BreakPoint.AI Game anaylsis

Want to develop a custom ai model to anaylze a person's tennis game and give them pointed feedback to improve on while also analyzing their play

### Input

Video of a person's tennis match / game/ set / point

### Output

Player Rating: A decimal value - Rating of the tennis play based on NTRP tennis rating
Coaching Recommendations: The 3 most relevant tips / drills to improve your game based on the video
Player type classification score: what kind of player are you (all are 0-1)

- BigServer
- ServeAndVolleyer
- AllCourtPlayer
- AttackingBaseliner
- SolidBaseliner
- CounterPuncher
