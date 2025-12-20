# BreakPoint.AI AI Analysis for Tennis

Using AI, generate a player profile and an improvement plan to get better at tennis

## Base AI Functionality for BreakPoint.AI Swing Coaching

Want to develop a custom ai model to analyze a person's tennis swing and give them pointed feedback to improve on.

### High Level Model Architecture Summary

The user inputs a video of a swing or a couple swings, it then chunks the swings into three phases (or more if needed) but at a minimum, backswing, swing, followthrough with preparation phases between swings. [see code in MoveNetProcessor.cs for a starting point] This uses movenet to harvest the joints so that we can get numeric data to actually feed into the AI. Then the model gets an output score and finds the top three (or x) features that were most negative and the top three most positive features. Each feature will need to be defined but currently are like joint position, velocity, acceleration, angles of certain joints, etc. 
(Note I am open to re-architecting if you think there is a better than a regression then feature identification to get actionable coaching tips)

Once it has those pieces of information and the usta score, send that to an LLM using semantic kernel to get actionable drills for that players level. We will expose tool calls to look at player history, other stats, etc.

### UI UX

The user uploads a video on the dashboard, gets notified once it is done, then can go the analysis page and see the key metrics it pulled. So they can see the recommended drills, maybe a video / gif / picture of a key moment pulled out from their uploaded content that can the focus point of the angular page

### Feature Architecture

The user uploads from the angular site -> goes to the api and creates an analysisRequest and uploads the video to blob storage -> azure function with queue trigger picks that up and goes to the database to get the specific analysisRequest attached [see AnalyzerFunction project] then calls the onxx model that was trained with the input data


### Training

I will provide videos with the labels decided on, those videos will go through the same swing phase rule-based detection code that the live swings go through.
