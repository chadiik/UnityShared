const on_git = require('./on_git');
const readline = require('readline');

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    prompt: 'chadiik-on-git> '
});

let branches = on_git.branches;
let options = '';
for(let i = 0; i < branches.length; i++) options += '\n\t' + i + ': ' + branches[i];
console.log('Available branches ->' + options);
console.log('Select a branch:');

rl.prompt();

rl.on('line', (line) => {

    let index = Number.parseInt(line.trim());

    if(index < 0 || index >= branches.length){

        console.log('Out of bounds!');
        prompt();
    }
    else{

        let branch = branches[index];
        if(on_git.installBranch(branch) == true){
    
            console.log(branch + ' installed.');
        }
        else{

            console.log('Could not complete...');
        }
    }

}).on('close', () => {

    process.exit(0);
});