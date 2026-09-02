\# Game Jam 2026 — Development Rules



\## Project

This is a 2D Unity game created for a game jam.



Unity version:

6000.0.26f1



\## Team

Member 1:

Technical / Systems



Member 2:

Gameplay / Level Design



Member 3:

Visual / UI / Audio



\## Architecture Rules



Use C# scripts with clear responsibilities.



Do not create duplicate managers.



Prefer existing components and systems over creating new ones.



Use Unity components such as:

\- Rigidbody2D

\- Collider2D

\- Animator

\- SpriteRenderer

\- Canvas



Use prefabs for reusable game objects.



Do not modify another developer's systems unnecessarily.



Do not create duplicate scripts that perform the same responsibility.



Keep scripts modular and readable.



Do not add packages unless explicitly requested.



Do not modify ProjectSettings unless necessary.



Before creating a new manager or system:

inspect the existing project first.



\## Git Rules



Never commit:

\- Library/

\- Temp/

\- Logs/

\- Build/

\- UserSettings/



Make small commits.



Do not reset or overwrite another developer's work.



\## Game Design



Prioritize:

1\. Gameplay

2\. Stability

3\. Theme

4\. Polish



The game must remain playable at all times.

