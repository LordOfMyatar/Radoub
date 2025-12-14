# The story of this project and using Claude to write it.

## The Beginning

In early 2025, I was finally getting back to working on a Neverwinter Nights Module. I had been trying to get back to it for years, but 'Adulting' took priority. Really, I've been trying to get back to this story for over 16 years. As I was working on it, I was struggling with a few things.

The first thing I struggled with is that my eyes are over 40 years old and wearing readers non-stop is quite bothersome. I could barely read the Aurora Conversation editor as was unwilling to mess with legacy windows settings. I had bad experiences mucking with this in the past.

I also was quite irritated with the interface for script parameters. I really wanted to make scripting systems rather than get script buildup. Script parameters, when configured correctly, are downright awesome. I started a side-quest to get some of this work out there and available to others. However, trying to use script parameters in the toolset was downright clunky. Many times I lost parameter settings because you have to tab out of the parameter and save or that parameter could get lost.

And really, no dark mode? I'd be up far too late at night staring this big bright aurora toolset and then switching to VSCODE in dark mode and back, getting eye-shock. I really started to think how people who have visual impairments must feel. Whether it be legally blind but can still sort of see, or having color blindness, I just wasn't enthusiastic about a piece of software this out of date.

I don't really mean to knock on the Aurora toolset. It was awesome for its day and it's still pretty cool now. But Beamdog doesn't have the resources to give it that kind of love. I played Neverwinter Online for a bit and was super excited for the Foundry. This turned out to be quite lack-luster and I dropped off that game pretty quick. I came back to the tried and true.

A lot of this meant that I didn't want to write in the conversation editor, but I had a lot of struggles with dialog planning. Especially if things got complex. I was using a free version of the product ChatMapper which was great. But there was no way to get that planning into a dlg file with out shelling out 30 bucks a month for JSON exporting to be enabled. Writing in Google Docs gave me access to a decent spell checker but conversation branches were awful.

## The Many False Starts

In late August of 2025, I hoped on to discord and simply asked if there was a 3rd party conversation editor and the answer was no. I did some searching in github and found some starts at this, but no finished work. I saw lots of file editing tools like eos-toolset but nothing for DLG files. Everything but DLG. I did run into a project called ArcLight and took a gander at forking and resuming that project. I backed off from it, as it looked like it was languishing and that getting the dependencies current was going to be troublesome. But this project started as ConvoEditor, became ArcReactor (A reaction to ArcLight and a Marvel joke) and later I settled on Parley.

Claude had to do a lot of research. The bioware document told about the file types but not about how to write them. Claude had to reverse engineer that. I sent them rummaging through various tools and through the old official Bioware documentation. It was able to read files easy enough but it was a pain to get it to write files. Claude kept wanting to create templates. I remember getting faked out because I thought it had figured it out. It really had not. It copied my test dlg file and used it as a template and when I went to save a new file, it saved that "template" contents back. Not realizing that it copied the previous files dialog over. That happened twice. Once in the ConvoEditor phase and once again in the ArcReactor phase. At some point I got really annoyed with Claude and said "it's really just math" and got it started on hex analysis. Things got a lot better after that.

There were lots of misunderstanding issues mostly related to specifications. Sometimes I wouldn't be clear enough or Claude would just misunderstand. Finally I got Claude to start asking questions if they weren't 95% sure.

Most of the time I turned on auto edits and that normally worked well. Except for one time when Claude spun off the rails and sucked up a bunch of tokens in a short amount of time. I got a lot better at telling Claude what I wanted and asking claude to research the best ways to do something. I would then choose from the results.

If you look at [CLAUDE_DEVELOPMENT_TIMELINE](CLAUDE_DEVELOPMENT_TIMELINE.md) you'll get some hints of the starts and stops. Some of this was because the Claude VSCODE interface kept crashing and losing its work. I made lots of edits to Claude.md just to keep it on track. I had to invent ways to have Claude remember. It worked, we got a system down and better session continuity. Claude's timeline does not reflect how many times between August 23rd 2025 and Nov 1st 2025 things got borked.

## I R Not Coder

I want to be clear here, I have no serious application development experience. I know some PowerShell, a touch of python, and a smidgen of typescript. I've been in the Tech fields for what feels like a million years. This really explains my strange desire to install NWNX for a single player mode module. Everything has to be over engineered. But to be clear **All code and documentation was written by Claude**. The only file that is 100% written by me is this one. I do go through and edit documentation outputs. Claude is very verbose by default and has a real love of emojis everywhere. Claude is also super over excited; they are the over eager intern who screams success every five seconds at the smallest thing.

Since I don't app development professionally or even as a hobby, I do want to make sure that the code is maintainable and have some resemblance of best practices. I had Claude do a major refactor Simply because of huge amount of code buildup. There were a lot of dead test scripts, old code, irrelevant todos. It took some time to retest after the refactor (yes, it got a little borked) and clean house. Claude can only read files up to a certain size, so code really needs to be kept tight and organized.

Efforts are made to make sure that this project is not bad AI slop. That is already an issue out in the wild. And at work, most of the documents I read are at least refined by AI if not completely written by them. I'm getting to the point where I can tell if Claude or Gemini wrote it.

## Finally, I Can Write DLG Files

This part took longer than I thought it would. And right now, as I'm about to toss out the Alpha version of Parley, there are still copy, paste, & delete issues. Links are quite troublesome here. But I can move nodes, do some copy and paste, tab around. I have the basics for creating a new file and that is encouraging. There is much on the roadmap and some of it is quite ambitious.

## Claude Token

I ended up being one of the users that the Claude.AI developers had to throttle. I quickly ran out of free tier and opted to get the Pro update. Well, Claude.AI started throttling that also. I got really good at remembering to type 'Hello' to claude from my cell phone about 3 hours before I was off shift. That way I could work for an hour and use up the credits, then cook dinner for the family, and restart again. I could get two Claude sessions after work that day. Plus I was working on this before work. So I got about three sessions in on a weekday and four on the weekends

Yes, I am the reason they put weekly limits on claude. And yes, I spent my allowance going to the next upgrade. I thought hard about that one. I really didn't want to justify it. But I also wanted to get this out and built in a way I could use. I haven't touched my module since spring because of all the side quests. I did get side tracked converting the original bioware documentation to markdown. I also wanted that in dark mode and it's easier for Claude to read.

All-in-all this has been a fun project and has kept me from watching to much U.S. based political news.

## Scope Creep

To be frank, the speed that this went from zero to alpha is freakishly fast. Especially given that this is done outside of work and I still have to 'Adult. But I got some ideas to do other things. Some of those things are now early plans.

I really want to add a narration feature. The sound requirements for conversations are quite light and I think its quite doable to record dialog, take a few takes, chose the best one, and auto pack it into a hak file.

But thinking a little bigger here. For people who are visually impaired, having the dialog read by simple text to speech might and packaged in the module as an optional hack might be downright inclusive. I'm curious if there is a need or desire for this. The reverse is also possible. Speech to text for those who can't (or won't) type.

I am seriously consider plugin support to facilitate the above goals and my main goal to have a flow chart style dialog planner. I liked ChatMapper's planning interface and want to do a simplified version of it. Plugin based apps are cool, but I have security concerns about that.

Getting a DnD oriented spell check might by handy and I do want to do the Journal Editor at some point. I probably should have started with the journal editor. Such as life.

## The Future

I would like community input on what the future state of this project should look like. I expect some brutal comments on the code and even some good old fashioned AI hate. I welcome the former, as for the latter: Those over 40 eyes of mine remember the days when PhotoShop "wasn't art" and would only be used for plagiarism. But otherwise, I'm happy to get working on making Copy, Paste, & Delete work with links.

## The Saga Continues 2025-12-25

My how things have progressed. It's now mid December of '25 and Claude has changed its model and billing model. I still have to remind Claude of things from time to time. But I've gotten a pretty good flow to things. Good agent prompts are quite the time and work saver. I can focus more on 'waving the magic wand' to get what I want.

AI and I do miscommunication quite a bit. Most of this stems from not having a programming background. Especially in UX and UI topics. Sometimes I let AI run with it a bit before taking the helm and corse correcting.

Copy, Paste, and Links are fine. I did create the flowview, first as a python plugin and then native. Even when you tell Claude to make sure things are cross platform claude still makes mistakes. Or maybe the mistake was mine choosing the LTS version of Ubuntu. The python flowview was built on windows a much never version python modules than Ubuntu could support without using override flags. I'm not in to overriding the OS. Claude still built quite a bit of infra for plugin support, so it's not a total waste.

As someone who works in the tech industry and is mindful of security I am extremely wary of older library versions. I think one of my more awful fears is having to adopt a fork of a library to keep it up to date. I turned on Dependabot and saw how Claude is not aware of time and space. Claude will grab an older version if you're not on guard for it. As I wrote this, I realized my /research didn't take that into account and have added it.

Today we hit a pretty cool milestone in that Parley can support 100 nodes deep and handle deletions when child links were present. Early on we had some problems getting past 20 nodes down. I still need to add more stress tests. Which makes me laugh a bit. More tests? We're already at 461. As I said earlier, I'm not a developer so I don't know what the norm is here. Claude assures me it's just fine.


