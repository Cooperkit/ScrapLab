-- ScrapLab's hidden input provider.
-- A Tool userdata is the only documented Lua API that exposes desired WASD
-- input independently of the character controller's collision response.

ScrapLabNoclipInputTool = class()

function ScrapLabNoclipInputTool.client_onUpdate( self, dt )
	if self.tool and self.tool:isLocal() then
		g_scrapLabNoclipToolInput = {
			move = self.tool:getRelativeMoveDirection(),
			direction = self.tool:getDirection(),
			speed = self.tool:getMovementSpeedFraction(),
			sprinting = self.tool:isSprinting()
		}
	end
end
