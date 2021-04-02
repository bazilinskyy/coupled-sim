%% Calculate the effect size of paired samples t-test: Cohen's D

function out = CohensD(data)
pair12 = data(:,1)-data(:,2); % baseline - mapping 1
pair23 = data(:,2)-data(:,3); % mapping 1 - mapping 2
pair13 = data(:,1)-data(:,3); % baseline - mapping 2

out = zeros(3,3);
out(1,:) = calcCohen(pair12);
out(2,:) = calcCohen(pair23);
out(3,:) = calcCohen(pair13);
end

%% helperfunction
function out = calcCohen(data)
m = mean(data);
s = std(data);
D = m/s;
out = [m, s, D];
end
