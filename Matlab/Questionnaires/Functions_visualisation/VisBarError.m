%% 
% Creates bar figure with errorbars and line between means
% Provide in order of passenger - pedestrian
% Note: create figure before calling this function

function VisBarError(meandata,errordata,namestring,ystr,titstr)
hold on;
colourcodes = [0, 0.4470, 0.7410;...
    0.8500, 0.3250, 0.0980;...
    0.9290, 0.6940, 0.1250;...
    0.4940, 0.1840, 0.5560;...
    0.4660, 0.6740, 0.1880;...
    0.3010, 0.7450, 0.9330;...
    0.6350, 0.0780, 0.1840;...
    0.25, 0.25, 0.25];
if(isempty(namestring))
    namestring = {'Passenger','Pedestrian'};
end
X = categorical(namestring);
X = reordercats(X,namestring);
for i = 1:length(X)
    h = bar(X(i),meandata(i));
    set(h, 'FaceColor', colourcodes(i,:));
end
ylabel(ystr); title(titstr);

% Mean line
plot(X,meandata,'LineWidth',2);

% Errorbar
er = errorbar(X,meandata,errordata,'CapSize',20);
er.Color = [0 0 0];
er.LineStyle = 'none';
grid on;

ax=gca; ax.FontSize = 15;
end

