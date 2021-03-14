%% Calculate crossing performance
% Crossing performance for the no-distraction yielding cases is defined as
% the percentage of button presses over the total phase period.

% Crossing performance  for the distraction cases and all the non-yielding
% cases is defined as 100 - the percentage of button presses over the total
% phase period

% Author: Johnson Mok
% Last Updated: 13-03-2021

function out = crossingPerformance(data, acpt_pa, acpt_pe)
buttonPerTrial = sumButtonPressPerTrial(data);

out.score.ND_Y = pressIsPositive(data.phasesPer.ND_Y, buttonPerTrial.ND_Y);
out.score.ND_NY = pressIsNegative(data.phasesPer.ND_NY, buttonPerTrial.ND_NY);
out.score.D_Y = pressIsNegative(data.phasesPer.D_Y, buttonPerTrial.D_Y);
out.score.D_NY = pressIsNegative(data.phasesPer.D_NY, buttonPerTrial.D_NY);

out.acpt.map0 = acpt_pe.Laan0.meanmean;
out.acpt.map1 = acpt_pe.Laan1.meanmean;
out.acpt.map2 = acpt_pe.Laan2.meanmean;

out.usefulness.map0 = acpt_pe.all.U0;
out.usefulness.map1 = acpt_pe.all.U1;
out.usefulness.map2 = acpt_pe.all.U2;

out.satisfying.map0 = acpt_pe.all.S0;
out.satisfying.map1 = acpt_pe.all.S1;
out.satisfying.map2 = acpt_pe.all.S2;
end

%% Helper function
function out = sumButtonPressPerTrial(data)
fld_con = fieldnames(data.phases);
for c=1:length(fld_con)
    fld_map = fieldnames(data.phases.(fld_con{c}));
    for m=1:length(fld_map)
        fld_phase = fieldnames(data.phases.(fld_con{c}).(fld_map{m}));
        for p=1:length(fld_phase)
            [nrows.(fld_con{c}).(fld_map{m}).(fld_phase{p}),ncols.(fld_con{c}).(fld_map{m}).(fld_phase{p})] = cellfun(@size,data.phases.(fld_con{c}).(fld_map{m}).(fld_phase{p}));
            val.(fld_con{c}).(fld_map{m}).(fld_phase{p}) = cellfun(@sum, data.phases.(fld_con{c}).(fld_map{m}).(fld_phase{p}));
            temp.(fld_con{c}).(fld_map{m})(p,:) = 100*val.(fld_con{c}).(fld_map{m}).(fld_phase{p})./nrows.(fld_con{c}).(fld_map{m}).(fld_phase{p});
        end
        if size(fld_phase,1) == 1 && size(fld_phase,2) == 1
            out.(fld_con{c}).(fld_map{m})= temp.(fld_con{c}).(fld_map{m});
        else
            out.(fld_con{c}).(fld_map{m}) = mean(temp.(fld_con{c}).(fld_map{m})); 
        end
    end
end
end

function out = pressIsPositive(data, button)
fld_map = fieldnames(data);
for m = 1:length(fld_map)
    out.(fld_map{m}).mean = mean(data.(fld_map{m}));
    out.(fld_map{m}).std = std(button.(fld_map{m})); 
end
end

function out = pressIsNegative(data,button)
fld_map = fieldnames(data);
for m = 1:length(fld_map)
    out.(fld_map{m}).mean = 100 - mean(data.(fld_map{m}));
    out.(fld_map{m}).std = std(100-button.(fld_map{m})); 
end
end
