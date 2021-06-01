%% analyzePedestrianGazeDirection
% This script creates an animation from the trial
% From the pedestrians POV:
% positive z is right, negative z is left
% positive x is in front, negative x is in back
% positive y is up, negative y is down
% rot is in degrees
% Author: Johnson Mok


function animateTrialsCombined(gap, pa_Dir, pa_Org, pe_Dir, pe_Org, pa_DirMean, pa_OrgMean, pe_DirMean, pe_OrgMean, diMat, titlestr, videoname) 
opengl hardware
debug = false;
%% Prepare data
dt = 0.0167;
time = (1:length(gap))*dt;
% Other
customColor = [0, 0.4470, 0.7410;...
    0.4660, 0.6740, 0.1880];
customColorMean = [0.75, 0.0, 0.75;...
    1.0, 0.0, 0.0];
customColorStd = [0.65, 0.20, 0.65;...
    0.8, 0.1, 0.1];
customColorDI = 'k';
%% Initialize video
myVideo = VideoWriter(videoname, 'Motion JPEG AVI'); %open video file
% myVideo.FrameRate = 59;  
myVideo.FrameRate = 30;  
myVideo.Quality = 100;
open(myVideo)

%% Pedestrian gaze animation
figure('WindowState','fullscreen');
subplot(2,2,[1,3])
% Image
cdata = flipdim( imread('VE.jpg'), 1 );
cdatar = flipdim( cdata, 2 );
surface([-120 -100; -120 -100], [3 3; 3 3], [-40 -40; 80 80], ...
    'FaceColor', 'texturemap', 'CData', cdata );
hold on
% Set up lines for all duo's
h = cell(size(pa_Dir.x,2)+5,1);
for k = 1:size(pa_Dir.x,2)
    h{k} = setUpLine(customColor,1); 
end
h{size(pa_Dir.x,2)+1} = setUpLine(customColorMean,4); % Mean
% h{size(pa_Dir.x,2)+2} = setUpLineSTD(customColorStd,2); % Mean-std
% h{size(pa_Dir.x,2)+3} = setUpLineSTD(customColorStd,2); % Mean+std
% h{size(pa_Dir.x,2)+4} = setUpLineSTD(customColorStd,2); % Mean-std bow
% h{size(pa_Dir.x,2)+5} = setUpLineSTD(customColorStd,2); % Mean+std bow
if (length(diMat.x)>1)
h{size(pa_Dir.x,2)+6} = setUpLineDI(customColorDI,4); % Distraction vehicle position
end
h{size(pa_Dir.x,2)+1} = setUpLine(customColorMean,4); % Buttonpress

axis([-120 -100 -5 5 -40 80]);
view(0,0) % XZ

set(gca,'xticklabel',[])
zticks([(-40:20:60)+17.19]);
zticklabels({'-40','-20','0','20','40','60'});
title(titlestr,'FontSize',15,'FontWeight','bold');
grid on;
zlabel('Distance from pedestrian in [m]','FontSize',12,'FontWeight','bold');
if debug == true
view([1 -1 5]);% debug ---------------
camup([0 1 0]);
xlabel('X') % -----------------
ylabel('Y') % -----------------
zlabel('Z') % -----------------
ylim([0 4]);
end
axis manual %// this line freezes the axes
alpha(0.4);
set(gca,'FontSize',25);

%% Gap acceptance
subplot(2,2,[2, 4]) 
g1 = animatedline('LineWidth', 4,'Color',customColor(1,:)); % Gap acceptance
hold on
g2 = plot(NaN, NaN, 'o','MarkerSize',8,'MarkerEdgeColor','k','MarkerFaceColor','k');  
xlabel('time in [s]','FontSize',12,'FontWeight','bold');
ylabel('Crossing button press in [%]','FontSize',12,'FontWeight','bold');
grid on;
ylim([0 100])
xlim([0 max(time)])
set(gca,'FontSize',25);

%% Animation
for n = 1:length(pe_Dir.x) % number of data points
    for j =1:size(pa_Dir.x,2)
        setLineData(n, pe_Org, pe_Dir, pa_Org, pa_Dir, h{j}, j);
        if (length(diMat.x)>1)
            setLineDataDI(n, diMat, h{size(pa_Dir.x,2)+6}, j)
        end
    end
    
    setLineData(n, pe_OrgMean, pe_DirMean, pa_OrgMean, pa_DirMean, h{size(pa_Dir.x,2)+1},1);
%     setLineDataSTD(n, pe_OrgMean, pe_DirMean, pa_OrgMean, pa_DirMean, h{size(pa_Dir.x,2)+2},2);
%     setLineDataSTD(n, pe_OrgMean, pe_DirMean, pa_OrgMean, pa_DirMean, h{size(pa_Dir.x,2)+3},3);
      
%     setLineStdToMean(n, pe_DirMean, pa_DirMean, h{size(pa_Dir.x,2)+4},2);
%     setLineStdToMean(n, pe_DirMean, pa_DirMean, h{size(pa_Dir.x,2)+5},3);
    
    addpoints(g1, time(n), gap(n)); % button press
    set(g2, 'XData', time(n), 'YData', gap(n));
    drawnow
%     pause(dt)
    
    frame = getframe(gcf); %get frame
    writeVideo(myVideo, frame);
end

close(myVideo)
close all
end

%% helper function
function h = setUpLine(customColor,w)
% w = linewidth
h{1} = plot3(NaN, NaN, NaN, '-','Color',customColor(1,:),'LineWidth',w); % pedestrian gaze line
h{2} = plot3(NaN, NaN, NaN, 'o','MarkerFaceColor',customColor(1,:),'MarkerEdgeColor',customColor(1,:),'LineWidth',w); % pedestrian position
h{3} = plot3(NaN, NaN, NaN, '-','Color',customColor(2,:),'LineWidth',w); % passenger gaze line
h{4} = plot3(NaN, NaN, NaN, 's','MarkerSize',6,'MarkerFaceColor',customColor(2,:),'MarkerEdgeColor',customColor(2,:),'LineWidth',w); % passenger position
end

function h = setUpLineSTD(customColor,w)
% w = linewidth
h{1} = plot3(NaN, NaN, NaN, '--','Color',customColor(1,:),'LineWidth',w); % pedestrian gaze line
h{2} = plot3(NaN, NaN, NaN, 'o','MarkerFaceColor',customColor(1,:),'MarkerEdgeColor',customColor(1,:),'LineWidth',w); % pedestrian position
h{3} = plot3(NaN, NaN, NaN, '--','Color',customColor(2,:),'LineWidth',w); % passenger gaze line
h{4} = plot3(NaN, NaN, NaN, 's','MarkerSize',6,'MarkerFaceColor',customColor(2,:),'MarkerEdgeColor',customColor(2,:),'LineWidth',w); % passenger position
end

function h = setUpLineDI(customColor,w)
% w = linewidth
h{1} = plot3(NaN, NaN, NaN, 's','MarkerSize',6,'MarkerFaceColor',customColor,'MarkerEdgeColor',customColor,'LineWidth',w); % distraction vehicle position
end

function setLineData(n, pe_Org, pe_Dir, pa_Org, pa_Dir, h, duoNr)
    i = duoNr;
        % Pedestian
        set(h{1}, 'XData', [pe_Org.x(n,i), pe_Dir.x(n,i)], 'YData', [pe_Org.y(n,i), pe_Dir.y(n,i)], 'ZData', [pe_Org.z(n,i), pe_Dir.z(n,i)]);
        set(h{2}, 'XData', pe_Org.x(n,i), 'YData', pe_Org.y(n,i), 'ZData', pe_Org.z(n,i));
        % Driver
        set(h{3}, 'XData', [pa_Org.x(n,i), pa_Dir.x(n,i)], 'YData', [pa_Org.y(n,i), pa_Dir.y(n,i)], 'ZData', [pa_Org.z(n,i), pa_Dir.z(n,i)]);
        set(h{4}, 'XData', pa_Org.x(n,i), 'YData', pa_Org.y(n,i), 'ZData', pa_Org.z(n,i));
        
%         % Pedestian
%         set(h{1}, 'XData', [pe_Org.x(n,i), pe_Dir.x(n,i)], 'YData', [0,0], 'ZData', [pe_Org.z(n,i), pe_Dir.z(n,i)]);
%         set(h{2}, 'XData', pe_Org.x(n,i), 'YData', 0, 'ZData', pe_Org.z(n,i));
%         % Driver
%         set(h{3}, 'XData', [pa_Org.x(n,i), pa_Dir.x(n,i)], 'YData', [0,0], 'ZData', [pa_Org.z(n,i), pa_Dir.z(n,i)]);
%         set(h{4}, 'XData', pa_Org.x(n,i), 'YData', 0, 'ZData', pa_Org.z(n,i));
end

function setLineDataSTD(n, pe_Org, pe_Dir, pa_Org, pa_Dir, h, duoNr)
    i = duoNr;
        % Pedestian
        set(h{1}, 'XData', [pe_Org.x(n,1), pe_Dir.x(n,i)], 'YData', [pe_Org.y(n,1), pe_Dir.y(n,i)], 'ZData', [pe_Org.z(n,1), pe_Dir.z(n,i)]);
        set(h{2}, 'XData', pe_Org.x(n,1), 'YData', pe_Org.y(n,1), 'ZData', pe_Org.z(n,1));
        % Driver
        set(h{3}, 'XData', [pa_Org.x(n,1), pa_Dir.x(n,i)], 'YData', [pa_Org.y(n,1), pa_Dir.y(n,i)], 'ZData', [pa_Org.z(n,1), pa_Dir.z(n,i)]);
        set(h{4}, 'XData', pa_Org.x(n,1), 'YData', pa_Org.y(n,1), 'ZData', pa_Org.z(n,1));
end

function setLineStdToMean(n, pe_mean, pa_mean, h, i)
        % Pedestian
        set(h{1}, 'XData', [pe_mean.x(n,i), pe_mean.x(n,1)], 'YData', [pe_mean.y(n,i), pe_mean.y(n,1)], 'ZData', [pe_mean.z(n,i), pe_mean.z(n,1)]);
        % Driver
        set(h{3}, 'XData', [pa_mean.x(n,i), pa_mean.x(n,1)], 'YData', [pa_mean.y(n,i), pa_mean.y(n,1)], 'ZData', [pa_mean.z(n,i), pa_mean.z(n,1)]);
end

function setLineDataDI(n, diPos, h, duoNr)
    i = duoNr;
        set(h{1}, 'XData', diPos.x(n,i), 'YData', diPos.y(n,i), 'ZData', diPos.z(n,i));
end
