# buildRank.tcl
# This script will allow you to set a minimum rank for building.
# Change the pointed-out line below to set the default rank.

namespace eval buildRank {
	
	# Change this line to set the default rank on startup
	set rankRequired "Admin"
	
	proc mayBuild {name} {
		variable rankRequired
		set rank [lindex [playerInfo $name] 6]
		set canBuild 0
		
		if {$rankRequired == "Guest"} {
			set canBuild 1
		} elseif {$rankRequired == "Builder"} {
			if {$rank != "Guest"} {
				set canBuild 1
			}
		} elseif {$rankRequired == "Mod"} {
			if {$rank == "Mod" || $rank == "Admin"} {
				set canBuild 1
			}
		} elseif {$rankRequired == "Admin"} {
			if {$rank == "Admin"} {
				set canBuild 1
			}
		}
		
		return $canBuild
	}

	proc changeBlockCallback {sender x y z newType oldType} {
		variable rankRequired
		if {![mayBuild $sender]} {
			setTile $x $y $z $oldType
			tell $sender "[getColorCode red]Only ranks $rankRequired and up may build!"
		}
	}
	
	proc changeBuildRank {newRank} {
		variable rankRequired
		if {$newRank == "Guest" || $newRank == "Builder" || $newRank == "Mod" || $newRank == "Admin"} {
			set rankRequired $newRank
			return 1
		} else {
			return 0
		}
	}
	
	proc init {} {
		scLog "BuildRank initialzed."
		onPlayerChangeBlock buildRank::changeBlockCallback
	}
	
	proc shutdown {} {
		scLog "BuildRank shut down."
		dropHook onPlayerChangeBlock buildRank::changeBlockCallback
	}
}

# Call this function via /tcl.
proc changeBuildRank {rank} {
	if {[buildRank::changeBuildRank $rank]} {
		return "Build rank set to $rank"
	} else {
		return "Invalid rank $rank, must be one of Guest, Builder, Mod, Admin."
	}
}

