# Station Science ReNewed (v3.0)

## Overview

**Station Science**, originally developed by **@ethernet**, adds large, advanced research parts for space stations to conduct long-term experiments. These experiments generate valuable Science points, helping you progress through the tech tree and expand your knowledge of the Kerbal universe. This mod is ideal for creating research stations orbiting various celestial bodies, requiring strategic planning and resource management.

The mod has undergone several updates, maintained by **@tomforwood** and **@linuxgurugamer**, and is now part of the **Spoony Industries** family under the development of **@spoonyboobah**. The acquisition of the Station Science Directorate by **Spoony Kerman** promises exciting new developments and additional funding for future research projects!

The mod has been completely revamped from the ground up, I have gone through all the code and restored it. The Arcanum Fuel Science pack has been removed as this was very bodged together and patched on top of StationScience. So I have removed it and it will not be returning due to me removing the ModuleCostlyScience from the mod as it essentially bodged the core code of StationScience.

---

## Parts and Experiments

### Key Parts:
- **TH-NKR Research Lab**: Generates *Eurekas* for Biology and all other experiments.
- **F-RRY Zoology Bay**: Generates *Bioproducts* for Physiology experiments, requiring *Kibbal*.
- **D-ZZY Cyclotron**: Generates *Kuarqs* for Physics experiments.

> **Note:** The **WT-SIT Spectrometron** is currently deprecated due to game balancing.

### New Part: Y-CKY Goo Chamber (Coming Soon!)
A new research module designed for bulk experimentation with **Mystery Goo™**. This specialized chamber will require significant quantities of Goo per experiment and will unlock a new **Chemistry** research chain.


### Experiment Pods:
Experiments are conducted using size 1 pods. These pods dock with your space station and are brought back to the surface or data can be transmitted back to Kerbin for Science points. Pods are mostly not re-runnable as once the data is transmitted you will receive no further Science Points by transmitting so you MUST return the pod for 100% of Science Points available, so plan accordingly. However, using a MPL you will be able to reset certain Experiment Pods for example, you have conducted a Nutritional Value experiment over Duna, once transmitted, you can reset the Pod using the MPL and then send it to Ike to do the experiment there... 

---

## Research Trees

Station Science experiments are divided into four core disciplines:
- **Biology (Plants)**
- **Physiology (Creatures)**
- **Physics (Kuarqs)**
- **Chemistry (Goo)** Coming Soon!

Each discipline has its own experiment tree that increases in complexity as you progress. Here's a breakdown of the experiment tiers:

| **Discipline**        | **Tier 1 - Basic**                     | **Tier 2 - Intermediate**                  | **Tier 3 - Hybrid/Advanced**                          |
|-----------------------|---------------------------------|-----------------------------|-------------------------------------|
| Biology / Plants       | Seed Growth                    | Nutritional Value            |
| Physiology / Creatures | Zoology Observations            | Creature Comforts            | Kuarq Bio-activity                  |
| Physics / Kuarqs       | Prograde Kuarqs                | Retrograde Kuarqs            | Eccentric Kuarqs               |


Each experiment requires resources such as *Eurekas*, *Bioproducts*, *Kuarqs*, or *Solutions* to complete. You’ll also need to finalize experiments by using the **Finalize Results** action, generating Science points. Make sure your space station stays in a stable orbit throughout the duration of the experiments to avoid aborting them.

### NEXT EXPERIMENT RELEASE: Irridiated Plant Adaptaion - Biology/Physics Hybrid Experiment - Tier 3 ###
Scientists have requested to conduct a experiment to see how your plants can adapt to hostile spacey environments and explore other space hazards. Your mission is to blast these potted plants with kuarqs and see how the plants and the kuarqs react to each other. You will also need to keep some poor creatures in the nearby to act as an medium for "unfortuante exposure" and study their bioproducts. You will need a TH-NKR Research Lab, a F-RRY Zoology Bay and 1x D-ZZY Cyclotron. Note: don't attach directly to your station, you MUST return it to the surface to complete. Subject to change.


### New Chemistry Research Chain: (Coming Soon!)
Explore the mysteries of **Mystery Goo™** with the **Y-CKY Goo Chamber**, unlocking a whole new branch of research related to *Chemistry*. This will introduce advanced Goo-related experiments and interactions with existing disciplines:


| **Discipline**        | **Tier 1**                     | **Tier 2**                  |
|-----------------------|---------------------------------|-----------------------------|
| Chemistry / Goo        | Mystery Goo™ Disclosure        | Goo Understanding            |

### Tier 4 Experiments (Coming Soon!)

Exciting new **Tier 4** experiments are currently in development and will add even more depth to your space station research! These advanced experiments will offer powerful Science rewards and unlock new scientific discoveries in each research discipline:

- **Biology / Plants**: Transgenic Agriculture
- **Physiology / Creatures**: Super Creature Genesis
- **Physics / Kuarqs**: Nu-Kuarq Particle Fusion
- **Chemistry / Goo**: Solved Goo™ Alchemy

Stay tuned for updates as these experiments will provide new challenges and further research opportunities in **Station Science**!


---

## Contracts

In **Career Mode**, contracts for station science experiments become available once you’ve visited a celestial body and unlocked the required labs and experiment pods. To complete these contracts:
1. Perform the experiment in orbit.
2. Return the experiment pod to Kerbin for recovery and completion.

### Upcoming Contract Overhaul: (Coming Soon!)
The contract system will be expanded, providing more opportunities for proper station-based science missions across the solar system.


### Private Experiments (Coming Soon for Career Mode Only!)
Also, new "Private" experiment pods will be coming soon... where a private science corporation has reached out to conduct a science experiment on your space station, this will reward you no science points, however the corp will payout handsomely for helping them with their research.

---

## Installation

1. Download the mod from [Spacedock](https://spacedock.info/mod/2670/MOARStation%20Science?ga=<Game+3102+'Kerbal+Space+Program).
2. Place the contents of the `GameData` folder into your KSP `GameData` directory.

---

## Potential Issues

1. If you have any bugs involving the buttons/fields in the Part Action Window acting strange and you have KSP Community Fixes installed, remove KSP CF and run the game without it, then reinstall. KSP CF has a PAW fix that caches PAW fields into memory and this can cause weird issues.
2. KSP crashing on installing StationScience?, fixed by deleting the ModuleManager cache as this will force the cache to be rebuilt.
3. You may get a log spam message of "[STNSCI-MOD] Error: 'status' field value is null." this is a bug with the stock KSP converter not properly having a "status" and yet it is still running fine. You can either ignore this but it will generate log spam. You can fix this by turning the relevant Lab Moudle off and on again to regenerate the "status".

---

## Licensing

- Source code and software (.cs and .dll files) are licensed under the GPL v3.
- Other assets (textures, models, etc.) are licensed under Creative Commons Attribution Share-Alike.

For more information, check out the [GitHub repository](https://github.com/SpoonyBoobah/StationScience).

---

## Contributors
- Originally created by **@ethernet**
- Updated to v2.0 by **@tomforwood**
- Maintained by **@linuxgurugamer**
- Now under active development by **@spoonyboobah** and **@SpeedyB64**
