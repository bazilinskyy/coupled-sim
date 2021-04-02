%% Calculate crossing performance
% Crossing performance for the no-distraction yielding cases is defined as
% the percentage of button presses over the total phase period.

% Crossing performance  for the distraction cases and all the non-yielding
% cases is defined as 100 - the percentage of button presses over the total
% phase period

% Author: Johnson Mok
% Last Updated: 13-03-2021

function out = crossingPerformance(data, acpt_pa, acpt_pe, trialorder)
buttonPerTrial = sumButtonPressPerTrial(data);
buttonPerPerson = meanButtonPerPerson(buttonPerTrial, trialorder);

out.score.ND_Y = pressIsPositive(data.phasesPer.ND_Y, buttonPerTrial.ND_Y);
out.score.ND_NY = pressIsNegative(data.phasesPer.ND_NY, buttonPerTrial.ND_NY);
out.score.D_Y = pressIsNegative(data.phasesPer.D_Y, buttonPerTrial.D_Y);
out.score.D_NY = pressIsNegative(data.phasesPer.D_NY, buttonPerTrial.D_NY);

buttonPerTrialWithoutStart = sumButtonPressPerTrialWithoutStart(data);
buttonPerPersonWithoutStart = meanButtonPerPerson(buttonPerTrialWithoutStart, trialorder);

out.score2.ND_Y = pressIsPositiveWithoutStart(data.phasesPer.ND_Y, buttonPerTrialWithoutStart.ND_Y);
out.score2.ND_NY = pressIsNegativeWithoutStart(data.phasesPer.ND_NY, buttonPerTrialWithoutStart.ND_NY);
out.score2.D_Y = pressIsNegativeWithoutStart(data.phasesPer.D_Y, buttonPerTrialWithoutStart.D_Y);
out.score2.D_NY = pressIsNegativeWithoutStart(data.phasesPer.D_NY, buttonPerTrialWithoutStart.D_NY);

out.acpt.map0 = acpt_pe.MeanStd_0;
out.acpt.map1 = acpt_pe.MeanStd_1;
out.acpt.map2 = acpt_pe.MeanStd_2;


out.usefulness.map0 = acpt_pe.all.U0;
out.usefulness.map1 = acpt_pe.all.U1;
out.usefulness.map2 = acpt_pe.all.U2;

out.satisfying.map0 = acpt_pe.all.S0;
out.satisfying.map1 = acpt_pe.all.S1;
out.satisfying.map2 = acpt_pe.all.S2;

out.r_acpt = pearsonsRAll(out.score, out.acpt);
out.r_U = pearsonsRAll(out.score, out.usefulness);
out.r_S = pearsonsRAll(out.score, out.satisfying);

% SPSS = SPSSmatrix(buttonPerPerson);
SPSS = SPSSmatrix(buttonPerPersonWithoutStart);
D_D_NY = CohensD(SPSS.D_NY);
D_D_Y = CohensD(SPSS.D_Y);
D_ND_NY = CohensD(SPSS.ND_NY);
D_ND_Y = CohensD(SPSS.ND_Y);

t_D_NY = pairedSamplesttest(SPSS.D_NY);
t_D_Y = pairedSamplesttest(SPSS.D_Y);
t_ND_NY = pairedSamplesttest(SPSS.ND_NY);
t_ND_Y = pairedSamplesttest(SPSS.ND_Y);


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
function out = meanButtonPerPerson(data, order)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        A = data.(fld_con{c}).(fld_map{m});
        B = order.(fld_con{c}).(fld_map{m}).Pnr;
        for i=1:max(B)
            out.(fld_con{c}).(fld_map{m})(i) = mean(A(find(B==i)));
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

function out = sumButtonPressPerTrialWithoutStart(data)
fld_con = fieldnames(data.phases);
for c=1:length(fld_con)
    fld_map = fieldnames(data.phases.(fld_con{c}));
    for m=1:length(fld_map)
        fld_phase = fieldnames(data.phases.(fld_con{c}).(fld_map{m}));
        for p=2:length(fld_phase)
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
function out = pressIsPositiveWithoutStart(data, button)
fld_map = fieldnames(data);
for m = 1:length(fld_map)
    out.(fld_map{m}).mean = mean(data.(fld_map{m})(2:end));
    out.(fld_map{m}).std = std(button.(fld_map{m})); 
end
end
function out = pressIsNegativeWithoutStart(data,button)
fld_map = fieldnames(data);
for m = 1:length(fld_map)
	out.(fld_map{m}).mean = 100 - mean(data.(fld_map{m})(2:end));
	out.(fld_map{m}).std = std(100-button.(fld_map{m}));     
end
end


function r = pearsonsR(performance, acceptance)
x = [performance.map0.mean; performance.map1.mean; performance.map2.mean];
y = [acceptance.map0(1); acceptance.map1(1); acceptance.map2(1)];

n = length(x);
num_1 = sum(x.*y);
num_2 = (sum(x)*sum(y))/n;
denum_1 = sqrt(sum(x.^2)-(sum(x)^2)/n);
denum_2 = sqrt(sum(y.^2)-(sum(y)^2)/n);

r = (num_1 - num_2) / (denum_1*denum_2);
% r2 = corrcoef(x,y); % same results
end
function out = pearsonsRAll(performance, acceptance)
fld_con = fieldnames(performance);
for c=1:length(fld_con)
    out.(fld_con{c}) = pearsonsR(performance.(fld_con{c}), acceptance);
end
end

function out = SPSSmatrix(data)
tablename = {'SPSS_Perf_ND_Y.csv','SPSS_Perf_ND_NY.csv','SPSS_Perf_D_Y.csv','SPSS_Perf_D_NY.csv'};
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    temp.(fld_con{c}) = NaN(length(data.ND_Y.map0),3);
    if strcmp(fld_con{c},'ND_Y')
        for m=1:length(fld_map)
            temp.(fld_con{c})(1:length(data.(fld_con{c}).(fld_map{m})),m) = data.(fld_con{c}).(fld_map{m});
        end
    else
        for m=1:length(fld_map)
            temp.(fld_con{c})(1:length(data.(fld_con{c}).(fld_map{m})),m) = 100-data.(fld_con{c}).(fld_map{m});
        end 
    end
    M = temp.(fld_con{c});
    T = array2table(M);
    T.Properties.VariableNames(1:3) = {'Baseline','Gaze_to_Yield','Look_Away_to_Yield'};
    writetable(T,tablename{c})
end
out = temp;
end

function out = pairedSamplesttest(data)
[~,p1,~,stats1] = ttest(data(:,1), data(:,2));
[~,p2,~,stats2] = ttest(data(:,2), data(:,3));
[~,p3,~,stats3] = ttest(data(:,1), data(:,3));
out = zeros(3,3);
out(1,:) = [stats1.tstat, stats1.df, p1];
out(2,:) = [stats2.tstat, stats2.df, p2];
out(3,:) = [stats3.tstat, stats3.df, p3];
end
function out = CohensD(data)
pair12 = data(:,1)-data(:,2); % baseline - mapping 1
pair23 = data(:,2)-data(:,3); % mapping 1 - mapping 2
pair13 = data(:,1)-data(:,3); % baseline - mapping 2

out = zeros(3,3);
out(1,:) = calcCohen(pair12);
out(2,:) = calcCohen(pair23);
out(3,:) = calcCohen(pair13);
end
function out = calcCohen(data)
m = mean(data);
s = std(data);
D = m/s;
out = [m, s, D];
end

